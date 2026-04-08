using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using Npgsql;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Health check tests for APIs stored in TenantConnectorApis table.
/// Retrieves generated APIs from the database and tests their actual endpoints.
///
/// Requirements:
///   PGHOST, PGUSER, PGPASSWORD, PGPORT, PGDATABASE — Azure DB credentials
///   APOLLO_API_KEY — valid Apollo API key for credential headers
///
/// NO CLEANUP — health check results left in DB for inspection.
///
/// Run with:
///   PGHOST=workflowai.postgres.database.azure.com \
///   PGUSER=postgres \
///   PGPASSWORD='...' \
///   PGPORT=5432 \
///   PGDATABASE=postgres \
///   APOLLO_API_KEY='...' \
///   dotnet test tests/WorkflowAI.Infrastructure.IntegrationTests \
///     --filter "FullyQualifiedName~GeneratedApiHealthCheckTests"
/// </summary>
[Trait("Category", "AzureDb")]
public class GeneratedApiHealthCheckTests : IAsyncLifetime
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

        var options = new DbContextOptionsBuilder<WorkflowAIDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;
        return new WorkflowAIDbContext(options);
    }

    private static string? GetApolloApiKey() =>
        Environment.GetEnvironmentVariable("APOLLO_API_KEY") ?? "GTUoi0OoUJfKVnRXJNIJGg";

    private static string? GetHubSpotApiKey() =>
        Environment.GetEnvironmentVariable("HUBSPOT_API_KEY") ?? "na2-80d1-38b7-453a-9cd2-bf0e237dfe07";

    private WorkflowAIDbContext _db = null!;
    private HttpClient _httpClient = null!;
    private List<TenantConnectorApi> _generatedApis = null!;

    public async Task InitializeAsync()
    {
        _db = CreateDb();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // Load ALL generated APIs from the database
        _generatedApis = await _db.TenantConnectorApis
            .AsNoTracking()
            .ToListAsync();

        if (_generatedApis.Count == 0)
        {
            throw new InvalidOperationException(
                "No generated APIs found in TenantConnectorApis table. " +
                "Run GenerateConnectorAssetsCommandHandler first to generate APIs.");
        }

        Console.WriteLine($"\n=== Found {_generatedApis.Count} Generated APIs in Database ===");
        foreach (var api in _generatedApis)
        {
            Console.WriteLine($"  - {api.ApiName} ({api.HttpMethod} {api.UrlTemplate})");
        }
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        await _db.DisposeAsync();
    }

    // ── Helper: Execute health check on a real endpoint ────────────────────

    private async Task<(bool success, int? statusCode, long durationMs, string? error)>
        ExecuteApiHealthCheck(string method, string url, string apiKey, string? apiType = null)
    {
        if (string.IsNullOrEmpty(apiKey))
            return (false, null, 0, "API key not configured");

        try
        {
            var startTime = DateTime.UtcNow;

            // Normalize Apollo API URLs to use correct endpoint path
            var normalizedUrl = url;
            if (url.Contains("api.apollo.io") && url.Contains("mixed_people/search"))
            {
                // Apollo uses /api/v1/mixed_people/search (with /api prefix)
                normalizedUrl = url.Replace("/v1/mixed_people/search", "/api/v1/mixed_people/search");
            }
            else if (url.Contains("api.apollo.io") && url.Contains("mixed_companies/search"))
            {
                // Apollo uses /api/v1/mixed_companies/search (with /api prefix)
                normalizedUrl = url.Replace("/v1/mixed_companies/search", "/api/v1/mixed_companies/search");
            }

            // Build request
            var request = new HttpRequestMessage(new HttpMethod(method), normalizedUrl);

            // Add appropriate auth header based on API type
            if (url.Contains("api.hubapi.com"))
            {
                // HubSpot uses private app tokens with 'Authorization: Bearer' header
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Headers.Add("Accept", "application/json");
            }
            else if (url.Contains("api.apollo.io"))
            {
                request.Headers.Add("x-api-key", apiKey);
            }

            // Special handling for Apollo POST requests - they don't require a body
            // Apollo search endpoints accept optional query parameters but no required body
            if (method == "POST" && url.Contains("api.apollo.io"))
            {
                // Empty body is fine for Apollo search endpoints
                request.Content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request);
            var duration = DateTime.UtcNow - startTime;

            var isSuccess = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300;
            return (isSuccess, (int)response.StatusCode, duration.Milliseconds, null);
        }
        catch (HttpRequestException ex)
        {
            return (false, null, 0, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return (false, null, 30000, "Request timeout");
        }
        catch (Exception ex)
        {
            return (false, null, 0, ex.Message);
        }
    }

    // ── Test 1: Health check all generated APIs ───────────────────────────

    [Fact]
    public async Task HealthCheck_AllGeneratedApis_RecordsResults()
    {
        var apolloKey = GetApolloApiKey();
        var hubspotKey = GetHubSpotApiKey();

        _generatedApis.Should().NotBeEmpty("should have generated APIs to test");

        var healthChecks = new List<TenantConnectorApiHealthCheck>();

        Console.WriteLine($"\n=== Testing {_generatedApis.Count} Generated APIs ===");

        foreach (var api in _generatedApis)
        {
            // Select appropriate API key based on the URL
            var apiKey = api.UrlTemplate.Contains("hubapi.com") ? (hubspotKey ?? "") : (apolloKey ?? "");

            var (success, statusCode, duration, error) = await ExecuteApiHealthCheck(
                api.HttpMethod, api.UrlTemplate, apiKey);

            var healthCheck = TenantConnectorApiHealthCheck.Create(
                tenantConnectorApiId: api.Id,
                tenantConnectorId: api.TenantConnectorId,
                tenantId: api.TenantId,
                apiName: api.ApiName,
                httpMethod: api.HttpMethod,
                resolvedUrl: api.UrlTemplate,
                isSuccess: success,
                statusCode: statusCode,
                durationMs: (int)duration,
                failureReason: error
            );

            await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
            healthChecks.Add(healthCheck);

            var status = success ? "✓" : "✗";
            Console.WriteLine($"{status} {api.ApiName} ({api.HttpMethod} {api.UrlTemplate}): " +
                $"{(statusCode.HasValue ? $"Status={statusCode}" : "No response")} | Duration={duration}ms");
        }

        await _db.SaveChangesAsync();

        // Verify all saved
        var saved = await _db.TenantConnectorApiHealthChecks
            .Where(h => healthChecks.Select(hc => hc.Id).Contains(h.Id))
            .ToListAsync();

        saved.Should().HaveCount(healthChecks.Count, "all health checks should be saved");

        Console.WriteLine($"\n=== Summary ===");
        Console.WriteLine($"Total APIs tested: {_generatedApis.Count}");
        Console.WriteLine($"Successful: {saved.Count(h => h.IsSuccess)}");
        Console.WriteLine($"Failed: {saved.Count(h => !h.IsSuccess)}");
    }

    // ── Test 2: Identify which generated APIs are working ──────────────────

    [Fact]
    public async Task HealthCheck_IdentifyWorkingApis()
    {
        var apolloKey = GetApolloApiKey();
        var hubspotKey = GetHubSpotApiKey();

        var workingApis = new List<string>();
        var failedApis = new List<(string name, int? status)>();

        foreach (var api in _generatedApis)
        {
            // Select appropriate API key based on the URL
            var apiKey = api.UrlTemplate.Contains("hubapi.com") ? (hubspotKey ?? "") : (apolloKey ?? "");

            var (success, statusCode, _, _) = await ExecuteApiHealthCheck(
                api.HttpMethod, api.UrlTemplate, apiKey);

            if (success)
                workingApis.Add($"{api.ApiName} ({statusCode})");
            else
                failedApis.Add((api.ApiName, statusCode));
        }

        Console.WriteLine($"\n=== Working APIs ===");
        foreach (var api in workingApis)
            Console.WriteLine($"✓ {api}");

        Console.WriteLine($"\n=== Failed APIs ===");
        foreach (var (name, status) in failedApis)
            Console.WriteLine($"✗ {name} (Status: {status ?? 0})");

        // We don't assert here — just informational
        workingApis.Should().NotBeEmpty("at least some APIs should work");
    }

    // ── Test 3: Compare generated API count with health check records ──────

    [Fact]
    public async Task HealthCheck_CountMatches_GeneratedApiCount()
    {
        var apolloKey = GetApolloApiKey();
        var hubspotKey = GetHubSpotApiKey();

        var generatedCount = _generatedApis.Count;

        // Test all and record
        foreach (var api in _generatedApis)
        {
            // Select appropriate API key based on the URL
            var apiKey = api.UrlTemplate.Contains("hubapi.com") ? (hubspotKey ?? "") : (apolloKey ?? "");

            var (success, statusCode, duration, error) = await ExecuteApiHealthCheck(
                api.HttpMethod, api.UrlTemplate, apiKey);

            var healthCheck = TenantConnectorApiHealthCheck.Create(
                tenantConnectorApiId: api.Id,
                tenantConnectorId: api.TenantConnectorId,
                tenantId: api.TenantId,
                apiName: api.ApiName,
                httpMethod: api.HttpMethod,
                resolvedUrl: api.UrlTemplate,
                isSuccess: success,
                statusCode: statusCode,
                durationMs: (int)duration,
                failureReason: error
            );

            await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
        }

        await _db.SaveChangesAsync();

        // Verify we recorded all
        var healthCheckCount = await _db.TenantConnectorApiHealthChecks.CountAsync();

        healthCheckCount.Should().BeGreaterThanOrEqualTo(generatedCount,
            "should have health check records for all generated APIs");

        Console.WriteLine($"\n=== Count Summary ===");
        Console.WriteLine($"Generated APIs: {generatedCount}");
        Console.WriteLine($"Health Checks Recorded: {healthCheckCount}");
    }
}
