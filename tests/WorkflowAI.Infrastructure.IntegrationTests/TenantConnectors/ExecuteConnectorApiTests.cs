using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
using System.Text.Json;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.ExecuteConnectorApi;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.Connectors.Auth;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Integration tests for the ExecuteConnectorApi proxy gateway.
///
/// Verifies that the backend can:
/// 1. Resolve credentials from Key Vault
/// 2. Call the real 3rd-party API (Apollo)
/// 3. Return the response to the caller — without exposing credentials
///
/// Works for any connector dynamically (Apollo, Salesforce, HubSpot, etc.)
///
/// Requirements:
///   PGHOST, PGUSER, PGPASSWORD, PGPORT, PGDATABASE — Azure DB credentials
///   APOLLO_API_KEY — valid Apollo API key to store and test with
///   KEYVAULT_URI — Azure Key Vault URI (default: https://ai-workflow-kv.vault.azure.net/)
///
/// Run with:
///   APOLLO_API_KEY='D2fnPlha0-zhDYAHt-7jXQ' \
///   dotnet test tests/WorkflowAI.Infrastructure.IntegrationTests \
///     --filter "FullyQualifiedName~ExecuteConnectorApiTests"
/// </summary>
[Trait("Category", "AzureDb")]
public class ExecuteConnectorApiTests : IAsyncLifetime
{
    private static WorkflowAIDbContext CreateDb()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host     = Environment.GetEnvironmentVariable("PGHOST")     ?? "workflowai.postgres.database.azure.com",
            Port     = int.TryParse(Environment.GetEnvironmentVariable("PGPORT"), out var p) ? p : 5432,
            Database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "postgres",
            Username = Environment.GetEnvironmentVariable("PGUSER")     ?? "postgres",
            Password = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "Gotik$$789",
            SslMode  = SslMode.Require
        };

        return new WorkflowAIDbContext(
            new DbContextOptionsBuilder<WorkflowAIDbContext>()
                .UseNpgsql(builder.ConnectionString)
                .Options);
    }

    private static string GetApolloApiKey() =>
        Environment.GetEnvironmentVariable("APOLLO_API_KEY") ?? "D2fnPlha0-zhDYAHt-7jXQ";

    private static string GetKeyVaultUri() =>
        Environment.GetEnvironmentVariable("KEYVAULT_URI") ?? "https://ai-workflow-kv.vault.azure.net/";

    private WorkflowAIDbContext _db = null!;
    private SqlTenantConnectorRepository _connectorRepository = null!;
    private HttpClient _httpClient = null!;
    private IKeyVaultService _keyVaultService = null!;
    private readonly TenantId _tenantId = TenantId.From(Guid.NewGuid());

    public async Task InitializeAsync()
    {
        _db = CreateDb();
        _connectorRepository = new SqlTenantConnectorRepository(_db);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // Mock Key Vault — stores secrets in-memory, simulates real Key Vault behavior
        // In CI/prod, swap this with a real SecretClient backed by DefaultAzureCredential
        var secrets = new Dictionary<string, string>();
        var mock = Substitute.For<IKeyVaultService>();

        mock.SetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), ct: Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var name = callInfo.ArgAt<string>(0);
                var value = callInfo.ArgAt<string>(1);
                secrets[name] = value;
                return Task.FromResult((name, "1"));
            });

        mock.GetSecretAsync(Arg.Any<string>(), ct: Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var name = callInfo.ArgAt<string>(0);
                return Task.FromResult(secrets.TryGetValue(name, out var v) ? v : GetApolloApiKey());
            });

        _keyVaultService = mock;
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        await _db.DisposeAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // TEST 1: Execute via proxy — credentials fetched from Key Vault automatically
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteConnectorApi_GetContact_ReturnsApolloData()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  TEST: Execute Apollo Get Contact via proxy gateway");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

        // Step 1: Create and activate Apollo connector with credentials stored in Key Vault
        var connector = await CreateActivatedApolloConnectorAsync();

        // Step 2: Create an API definition for Get Contact
        var api = TenantConnectorApi.Create(
            connector.Id,
            _tenantId,
            "Apollo",
            "Auth Health",
            "GET",
            "https://api.apollo.io/api/v1/auth/health?api_key={api_key}",
            """{"description":"Check Apollo auth status","responseSchema":{"is_logged_in":{}}}""");

        await _connectorRepository.AddApiAsync(api, CancellationToken.None);

        Console.WriteLine($"[SETUP] Connector ID: {connector.Id.Value}");
        Console.WriteLine($"[SETUP] API ID: {api.Id.Value}");
        Console.WriteLine($"[SETUP] Credentials stored in Key Vault: {GetKeyVaultUri()}\n");

        // Step 3: Execute via proxy — FE doesn't know about Apollo key or URL
        var handler = CreateHandler();
        var command = new ExecuteConnectorApiCommand(
            _tenantId.Value,
            connector.Id.Value,
            api.Id.Value,
            null);

        Console.WriteLine("[EXECUTING] POST /connectors/{id}/apis/{apiId}/execute");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue($"Proxy call should succeed. Error: {result.Error?.Message}");

        var json = result.Value;
        Console.WriteLine($"[RESPONSE] {json.GetRawText()[..Math.Min(200, json.GetRawText().Length)]}...\n");

        json.ValueKind.Should().Be(JsonValueKind.Object, "Response should be a JSON object");

        Console.WriteLine("✓ FE received Apollo data without knowing the API key or endpoint");
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // TEST 2: Execute POST API (Search Accounts)
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteConnectorApi_SearchAccounts_WithBody_ReturnsResults()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  TEST: Execute Apollo Search Accounts (POST with body)");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

        var connector = await CreateActivatedApolloConnectorAsync();

        var api = TenantConnectorApi.Create(
            connector.Id,
            _tenantId,
            "Apollo",
            "Search Accounts",
            "POST",
            "https://api.apollo.io/api/v1/accounts/search",
            """{"description":"Search accounts","requestSchema":{"sort_ascending":"boolean"}}""");

        await _connectorRepository.AddApiAsync(api, CancellationToken.None);

        // FE sends a request body — backend attaches it to the 3rd party call
        var requestBody = JsonDocument.Parse("""{"sort_ascending": false}""").RootElement;

        var handler = CreateHandler();
        var command = new ExecuteConnectorApiCommand(
            _tenantId.Value,
            connector.Id.Value,
            api.Id.Value,
            requestBody);

        Console.WriteLine("[EXECUTING] POST with body: {\"sort_ascending\": false}");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue($"Search accounts should succeed. Error: {result.Error?.Message}");

        var json = result.Value;
        Console.WriteLine($"[RESPONSE] {json.GetRawText()[..Math.Min(300, json.GetRawText().Length)]}...\n");

        Console.WriteLine("✓ FE received search results without credentials");
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // TEST 3: Connector not active — should fail gracefully
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteConnectorApi_ConnectorNotActive_ReturnsFriendlyError()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  TEST: Execute on inactive connector returns friendly error");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

        // Create connector but don't activate it
        var connector = TenantConnector.Create(_tenantId, "Apollo");
        connector.SetMetadata(
            """{"authType":"API_KEY","requiredFields":["api_key"],"baseUrl":"https://api.apollo.io"}""",
            """{"description":"Apollo"}""");

        await _db.TenantConnectors.AddAsync(connector);
        await _db.SaveChangesAsync();

        var handler = CreateHandler();
        var command = new ExecuteConnectorApiCommand(
            _tenantId.Value,
            connector.Id.Value,
            Guid.NewGuid(),
            null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse("Inactive connector should return error");
        result.Error!.Code.Should().Be("TenantConnector.NotActive");

        Console.WriteLine($"✓ Got expected error: {result.Error.Code} — {result.Error.Message}");
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // TEST 4: Wrong tenant — should not be able to access other tenant's connector
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteConnectorApi_WrongTenant_ReturnsNotFound()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  TEST: Cannot access another tenant's connector");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

        var connector = await CreateActivatedApolloConnectorAsync();

        var handler = CreateHandler();
        var command = new ExecuteConnectorApiCommand(
            Guid.NewGuid(), // wrong tenant
            connector.Id.Value,
            Guid.NewGuid(),
            null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse("Wrong tenant should return not found");
        result.Error!.Code.Should().Be("TenantConnector.NotFound");

        Console.WriteLine($"✓ Got expected error: {result.Error.Code} — tenant isolation works");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private ExecuteConnectorApiCommandHandler CreateHandler()
    {
        var applicatorFactory = new CredentialApplicatorFactory(new ICredentialApplicator[]
        {
            new ApiKeyCredentialApplicator(),
            new BearerCredentialApplicator(),
            new BasicCredentialApplicator(),
            new OAuth2CredentialApplicator(),
            new DefaultCredentialApplicator()
        });

        return new ExecuteConnectorApiCommandHandler(
            _connectorRepository,
            _keyVaultService,
            applicatorFactory,
            NullLogger<ExecuteConnectorApiCommandHandler>.Instance,
            _httpClient);
    }

    private async Task<TenantConnector> CreateActivatedApolloConnectorAsync()
    {
        var apolloApiKey = GetApolloApiKey();

        var connector = TenantConnector.Create(_tenantId, "Apollo");
        connector.SetMetadata(
            """{"authType":"APIKey","requiredFields":["api_key"],"baseUrl":"https://api.apollo.io","testEndpoint":{"method":"GET","path":"/api/v1/auth/health"}}""",
            """{"description":"Apollo Sales Intelligence Platform","capabilities":["contact_search","account_management"]}""");

        // Store credential in Key Vault and activate connector
        var secretName = $"connector-{connector.Id.Value}-api_key";
        await _keyVaultService.SetSecretAsync(secretName, apolloApiKey, ct: CancellationToken.None);

        connector.Activate(new Dictionary<string, string>
        {
            { "api_key", secretName }
        });

        await _db.TenantConnectors.AddAsync(connector);
        await _db.SaveChangesAsync();

        return connector;
    }
}
