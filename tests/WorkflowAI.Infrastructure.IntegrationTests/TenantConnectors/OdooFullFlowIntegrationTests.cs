using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Full flow integration test for the Odoo connector — no Claude SDK calls.
/// All connector data is read from the database; credentials come from local.settings.json.
///
/// Flow:
///   Step 1 — Provision check:  Assert connector exists in DB with valid metadata.
///   Step 2 — Validate:         Authenticate to Odoo XML-RPC using stored credentials.
///   Step 3 — Get APIs:         Load TenantConnectorApis from DB for this connector.
///   Step 4 — Health check:     XML-RPC search_read per API; log to TenantConnectorApiHealthChecks.
///
/// Prerequisites:
///   - Odoo connector must already be provisioned (run OdooApisAzureDbTests or provision via API).
///   - TenantConnectorApis must be seeded (run OdooApisAzureDbTests.Odoo_CleanAndReseedApiOperations).
///
/// Run:
///   PGPASSWORD='Gotik$$789' dotnet test tests/WorkflowAI.Infrastructure.IntegrationTests \
///     --filter "FullyQualifiedName~OdooFullFlowIntegrationTests" -v normal
/// </summary>
[Trait("Category", "AzureDb")]
public class OdooFullFlowIntegrationTests : IAsyncLifetime
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private static WorkflowAIDbContext CreateDb()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host     = Environment.GetEnvironmentVariable("PGHOST")     ?? "workflowai.postgres.database.azure.com",
            Port     = int.TryParse(Environment.GetEnvironmentVariable("PGPORT"), out var p) ? p : 5432,
            Database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "postgres",
            Username = Environment.GetEnvironmentVariable("PGUSER")     ?? "postgres",
            Password = Environment.GetEnvironmentVariable("PGPASSWORD")
                       ?? throw new InvalidOperationException("PGPASSWORD env var is not set."),
            SslMode  = SslMode.Require
        };
        return new WorkflowAIDbContext(
            new DbContextOptionsBuilder<WorkflowAIDbContext>().UseNpgsql(builder.ConnectionString).Options);
    }

    /// <summary>
    /// Reads Odoo credentials from env vars or local.settings.json.
    /// Required keys: api_key, instance_url, user_login.
    /// Returns (apiKey, instanceUrl, userLogin).
    /// </summary>
    private static (string ApiKey, string InstanceUrl, string UserLogin) ResolveCredentials()
    {
        var apiKey      = Environment.GetEnvironmentVariable("ODOO_API_KEY");
        var instanceUrl = Environment.GetEnvironmentVariable("ODOO_INSTANCE_URL");
        var login       = Environment.GetEnvironmentVariable("ODOO_USER_LOGIN");

        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(instanceUrl) && !string.IsNullOrEmpty(login))
            return (apiKey, instanceUrl.TrimEnd('/'), login);

        // Fall back to local.settings.json → Connector:Credentials:Odoo
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var file = Path.Combine(dir.FullName, "src", "WorkflowAI.Functions", "local.settings.json");
            if (File.Exists(file))
            {
                var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.TryGetProperty("Connector", out var c) &&
                    c.TryGetProperty("Credentials", out var creds) &&
                    creds.TryGetProperty("Odoo", out var odoo))
                {
                    var json = odoo.GetString()
                        ?? throw new InvalidOperationException("Odoo credentials value is null in local.settings.json");

                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;

                    if (!dict.TryGetValue("api_key",      out var k))  throw new InvalidOperationException("Missing 'api_key' in Odoo credentials.");
                    if (!dict.TryGetValue("instance_url", out var u))  throw new InvalidOperationException("Missing 'instance_url' in Odoo credentials.");
                    if (!dict.TryGetValue("user_login",   out var l))  throw new InvalidOperationException("Missing 'user_login' in Odoo credentials. Add it to local.settings.json.");

                    return (k, u.TrimEnd('/'), l);
                }
                break;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Odoo credentials not found. Set ODOO_API_KEY + ODOO_INSTANCE_URL + ODOO_USER_LOGIN env vars, " +
            "or add Connector:Credentials:Odoo (with api_key, instance_url, user_login) in local.settings.json.");
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private WorkflowAIDbContext _db = null!;
    private SqlTenantConnectorApiHealthCheckRepository _healthRepo = null!;
    private TenantConnector _connector = null!;
    private string _apiKey = null!;
    private string _instanceUrl = null!;
    private string _userLogin = null!;
    private string _dbName = null!;   // derived from instance URL hostname
    private int _uid;

    public async Task InitializeAsync()
    {
        _db = CreateDb();
        _healthRepo = new SqlTenantConnectorApiHealthCheckRepository(_db);
        (_apiKey, _instanceUrl, _userLogin) = ResolveCredentials();
        _dbName = new Uri(_instanceUrl).Host.Split('.')[0];

        // Load connector — tests will assert/use this
        _connector = await _db.TenantConnectors
            .FirstOrDefaultAsync(c => c.ConnectorType == "Odoo")
            ?? throw new InvalidOperationException(
                "Odoo connector not found in DB. Run OdooApisAzureDbTests or provision via API first.");
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    // ── Step 1: Provision check ───────────────────────────────────────────────

    [Fact]
    public void Step1_ProvisionCheck_ConnectorExistsWithValidMetadata()
    {
        Console.WriteLine($"\n>>> [Step 1] Provision check");
        Console.WriteLine($"    ConnectorId  : {_connector.Id.Value}");
        Console.WriteLine($"    ConnectorType: {_connector.ConnectorType}");
        Console.WriteLine($"    Status       : {_connector.Status.Name}");
        Console.WriteLine($"    TenantId     : {_connector.TenantId.Value}");

        _connector.ConnectorType.Should().Be("Odoo");
        _connector.Metadata.Should().NotBeNullOrEmpty("metadata must be stored after provisioning");

        // Metadata must be valid JSON with required fields
        var meta = JsonDocument.Parse(_connector.Metadata).RootElement;
        meta.TryGetProperty("authType",      out var authType).Should().BeTrue("authType required");
        meta.TryGetProperty("baseUrl",       out var baseUrl).Should().BeTrue("baseUrl required");
        meta.TryGetProperty("testEndpoint",  out _).Should().BeTrue("testEndpoint required");

        Console.WriteLine($"    AuthType     : {authType.GetString()}");
        Console.WriteLine($"    BaseUrl      : {baseUrl.GetString()}");
        Console.WriteLine($"    Info         : {_connector.Info[..Math.Min(80, _connector.Info.Length)]}...");
        Console.WriteLine($">>> Step 1 PASSED");
    }

    // ── Step 2: Validate — authenticate to Odoo XML-RPC ─────────────────────

    [Fact]
    public async Task Step2_Validate_AuthenticatesAndConnectorIsActive()
    {
        Console.WriteLine($"\n>>> [Step 2] Validate connector — XML-RPC authenticate");
        Console.WriteLine($"    InstanceUrl : {_instanceUrl}");
        Console.WriteLine($"    UserLogin   : {_userLogin}");
        Console.WriteLine($"    DbName      : {_dbName}");

        // Parse testEndpoint from connector metadata
        var meta = JsonDocument.Parse(_connector.Metadata).RootElement;
        var testPath = meta.TryGetProperty("testEndpoint", out var ep) &&
                       ep.TryGetProperty("path", out var path)
            ? path.GetString() : "/xmlrpc/2/common";

        var testUrl = $"{_instanceUrl}{testPath}";
        Console.WriteLine($"    TestEndpoint: {testUrl}");

        // Call authenticate — proves credentials work
        _uid = await XmlRpcAuthenticateAsync(_instanceUrl, _dbName, _userLogin, _apiKey);

        Console.WriteLine($"    Auth uid    : {_uid}");

        _uid.Should().BeGreaterThan(0, "authenticate must return a valid UID");

        // Connector should be Active (was activated during seeding)
        _connector.Status.Should().Be(TenantConnectorStatus.Active,
            "connector must be Active before generating assets");

        Console.WriteLine($">>> Step 2 PASSED — authenticated as uid={_uid}");
    }

    // ── Step 3: Get APIs from DB ──────────────────────────────────────────────

    [Fact]
    public async Task Step3_GetApis_ReturnsApiListFromDatabase()
    {
        Console.WriteLine($"\n>>> [Step 3] Get APIs from TenantConnectorApis");

        var apis = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _connector.Id)
            .OrderBy(a => a.ApiName)
            .ToListAsync();

        apis.Should().NotBeEmpty(
            "APIs must be seeded first — run OdooApisAzureDbTests.Odoo_CleanAndReseedApiOperations");

        Console.WriteLine($"    Found {apis.Count} APIs for connector {_connector.ConnectorType}:\n");
        foreach (var api in apis)
        {
            var meta = SafeParseModelMethod(api.Metadata);
            Console.WriteLine($"    {api.ApiName,-30} → {meta.Model}.{meta.Method}");
        }

        // All should be POST (XML-RPC)
        apis.Should().AllSatisfy(a => a.HttpMethod.Should().Be("POST"));

        Console.WriteLine($"\n>>> Step 3 PASSED — {apis.Count} APIs loaded");
    }

    // ── Step 4: Health check each API via XML-RPC ────────────────────────────

    [Fact]
    public async Task Step4_HealthCheckAllApis_LogsResultsToDatabase()
    {
        // Authenticate first
        _uid = await XmlRpcAuthenticateAsync(_instanceUrl, _dbName, _userLogin, _apiKey);
        _uid.Should().BeGreaterThan(0);

        var apis = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _connector.Id)
            .OrderBy(a => a.ApiName)
            .ToListAsync();

        apis.Should().NotBeEmpty("run Step 3 / reseed APIs first");

        // Always use the canonical XML-RPC object endpoint regardless of what UrlTemplate says.
        // Older AI-generated records may have /web/dataset/call_kw/... which is JSON-RPC (wrong).
        var xmlRpcObjectUrl = $"{_instanceUrl}/xmlrpc/2/object";

        Console.WriteLine($"\n>>> [Step 4] Health checking {apis.Count} Odoo APIs via XML-RPC");
        Console.WriteLine($"    Connector : {_connector.ConnectorType} ({_connector.Id.Value})");
        Console.WriteLine($"    Endpoint  : {xmlRpcObjectUrl}");
        Console.WriteLine($"    UID       : {_uid}\n");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var checks = new List<TenantConnectorApiHealthCheck>();

        foreach (var api in apis)
        {
            var resolvedUrl = xmlRpcObjectUrl;

            var (model, method) = SafeParseModelMethod(api.Metadata);

            // For mutating methods use search_read to avoid side effects during health checks
            var testBody = BuildXmlRpcSearchRead(_dbName, _uid, _apiKey, model);

            var sw = Stopwatch.StartNew();
            bool isSuccess;
            int? statusCode = null;
            string? failureReason = null;

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, resolvedUrl);
                req.Content = new StringContent(testBody, Encoding.UTF8, "text/xml");

                var response = await http.SendAsync(req);
                sw.Stop();

                statusCode = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    isSuccess     = false;
                    failureReason = $"HTTP {statusCode}: {response.ReasonPhrase}";
                }
                else if (body.Contains("<fault>"))
                {
                    isSuccess     = false;
                    failureReason = ExtractXmlRpcFault(body);
                }
                else
                {
                    isSuccess = true;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                isSuccess     = false;
                failureReason = ex.Message[..Math.Min(200, ex.Message.Length)];
            }

            var icon = isSuccess ? "OK  " : "FAIL";
            var detail = failureReason is not null
                ? $"  [{failureReason[..Math.Min(80, failureReason.Length)]}]"
                : string.Empty;
            Console.WriteLine(
                $"    {icon} {api.ApiName,-30} ({model}.{method,-15}) → {statusCode} ({sw.ElapsedMilliseconds}ms){detail}");

            checks.Add(TenantConnectorApiHealthCheck.Create(
                api.Id, _connector.Id, _connector.TenantId,
                api.ApiName, api.HttpMethod, resolvedUrl,
                isSuccess, statusCode, sw.ElapsedMilliseconds, failureReason));
        }

        await _healthRepo.AddRangeAsync(checks);

        var successCount = checks.Count(c => c.IsSuccess);
        var failCount    = checks.Count(c => !c.IsSuccess);

        Console.WriteLine($"\n>>> Results: {successCount} succeeded, {failCount} failed");
        Console.WriteLine($">>> {checks.Count} rows logged to TenantConnectorApiHealthChecks");

        checks.Should().NotBeEmpty();
        successCount.Should().BeGreaterThan(0,
            "at least core Odoo models (res.partner, sale.order) should be accessible");

        // Verify rows persisted
        var saved = await _healthRepo.GetByConnectorAsync(_connector.Id, pageSize: 200);
        saved.Should().HaveCountGreaterThanOrEqualTo(checks.Count);

        Console.WriteLine($">>> Step 4 PASSED");
    }

    // ── Full end-to-end (all steps in order) ─────────────────────────────────

    [Fact]
    public async Task FullFlow_AllSteps_Odoo()
    {
        Console.WriteLine("\n════════════════════════════════════════════════════════");
        Console.WriteLine("  Odoo Full Flow Integration Test");
        Console.WriteLine("════════════════════════════════════════════════════════");

        // Step 1 — Provision check
        Step1_ProvisionCheck_ConnectorExistsWithValidMetadata();

        // Step 2 — Validate (authenticate)
        await Step2_Validate_AuthenticatesAndConnectorIsActive();

        // Step 3 — Get APIs
        await Step3_GetApis_ReturnsApiListFromDatabase();

        // Step 4 — Health check all APIs
        await Step4_HealthCheckAllApis_LogsResultsToDatabase();

        Console.WriteLine("\n════════════════════════════════════════════════════════");
        Console.WriteLine("  All steps PASSED");
        Console.WriteLine("════════════════════════════════════════════════════════");
    }

    // ── XML-RPC helpers ───────────────────────────────────────────────────────

    private static async Task<int> XmlRpcAuthenticateAsync(
        string instanceUrl, string db, string login, string apiKey)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var xml = $"""
            <?xml version="1.0"?>
            <methodCall>
              <methodName>authenticate</methodName>
              <params>
                <param><value><string>{db}</string></value></param>
                <param><value><string>{login}</string></value></param>
                <param><value><string>{apiKey}</string></value></param>
                <param><value><struct/></value></param>
              </params>
            </methodCall>
            """;

        var response = await http.PostAsync(
            $"{instanceUrl}/xmlrpc/2/common",
            new StringContent(xml, Encoding.UTF8, "text/xml"));

        var body = await response.Content.ReadAsStringAsync();
        var doc  = new XmlDocument();
        doc.LoadXml(body);
        var node = doc.SelectSingleNode("//methodResponse/params/param/value/int");
        if (node is null)
            throw new InvalidOperationException($"Odoo authenticate failed. Response: {body[..Math.Min(500, body.Length)]}");
        return int.Parse(node.InnerText);
    }

    private static string BuildXmlRpcSearchRead(string db, int uid, string apiKey, string model)
    {
        return $"""
            <?xml version="1.0"?>
            <methodCall>
              <methodName>execute_kw</methodName>
              <params>
                <param><value><string>{db}</string></value></param>
                <param><value><int>{uid}</int></value></param>
                <param><value><string>{apiKey}</string></value></param>
                <param><value><string>{model}</string></value></param>
                <param><value><string>search_read</string></value></param>
                <param><value><array><data>
                  <value><array><data></data></array></value>
                </data></array></value></param>
                <param><value><struct>
                  <member><name>fields</name><value><array><data>
                    <value><string>id</string></value>
                    <value><string>name</string></value>
                  </data></array></value></member>
                  <member><name>limit</name><value><int>1</int></value></member>
                </struct></value></param>
              </params>
            </methodCall>
            """;
    }

    private static (string Model, string Method) SafeParseModelMethod(string metadataJson)
    {
        try
        {
            var doc    = JsonDocument.Parse(metadataJson).RootElement;
            var model  = doc.TryGetProperty("model",  out var m)  ? m.GetString()  ?? "res.partner" : "res.partner";
            var method = doc.TryGetProperty("method", out var mt) ? mt.GetString() ?? "search_read"  : "search_read";
            return (model, method);
        }
        catch { return ("res.partner", "search_read"); }
    }

    private static string ExtractXmlRpcFault(string responseBody)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(responseBody);
            var msg = doc.SelectSingleNode("//fault//member[name='faultString']/value/string");
            return msg?.InnerText[..Math.Min(200, msg.InnerText.Length)] ?? "XML-RPC fault";
        }
        catch { return "XML-RPC fault (parse error)"; }
    }
}
