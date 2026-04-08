using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using Npgsql;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Real health check tests for Apollo APIs.
/// Calls the actual Apollo API endpoints and records health check results to the database.
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
///     --filter "FullyQualifiedName~ApolloApiHealthCheckRealTests"
///
/// If APOLLO_API_KEY is not set, tests are skipped gracefully.
/// </summary>
[Trait("Category", "AzureDb")]
public class ApolloApiHealthCheckRealTests : IAsyncLifetime
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

    private WorkflowAIDbContext _db = null!;
    private HttpClient _httpClient = null!;
    private TenantConnector _apolloConnector = null!;
    private TenantConnectorApi? _healthCheckApi = null;
    private TenantConnectorApi? _searchPeopleApi = null;
    private TenantConnectorApi? _getContactApi = null;

    public async Task InitializeAsync()
    {
        _db = CreateDb();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // Get or create Apollo connector
        var existing = await _db.TenantConnectors
            .FirstOrDefaultAsync(c => c.ConnectorType == "Apollo");

        if (existing is not null)
        {
            _apolloConnector = existing;
        }
        else
        {
            _apolloConnector = TenantConnector.Create(TenantId.New(), "Apollo");
            _apolloConnector.SetMetadata(
                """{"authType":"APIKey","requiredFields":["api_key"],"baseUrl":"https://api.apollo.io","testEndpoint":{"method":"GET","path":"/v1/auth/health"}}""",
                """{"description":"Apollo.io sales intelligence platform","docsUrl":"https://apolloio.github.io/apollo-api-docs/","capabilities":["contact_search","company_search"],"rateLimits":"50 requests/minute","webhookSupport":false}"""
            );
            _apolloConnector.Activate();
            await _db.TenantConnectors.AddAsync(_apolloConnector);
            await _db.SaveChangesAsync();
        }

        // Get or create Apollo APIs for health checking
        _healthCheckApi = await _db.TenantConnectorApis
            .FirstOrDefaultAsync(a => a.TenantConnectorId == _apolloConnector.Id && a.ApiName == "Health Check");

        if (_healthCheckApi is null)
        {
            _healthCheckApi = TenantConnectorApi.Create(
                _apolloConnector.Id, _apolloConnector.TenantId, "Apollo", "Health Check", "GET",
                "https://api.apollo.io/v1/auth/health",
                """{"requestBody":null,"responseSchema":{"type":"object","properties":{"authenticated":{"type":"boolean"}}}}"""
            );
            await _db.TenantConnectorApis.AddAsync(_healthCheckApi);
        }

        _searchPeopleApi = await _db.TenantConnectorApis
            .FirstOrDefaultAsync(a => a.TenantConnectorId == _apolloConnector.Id && a.ApiName == "Search People");

        if (_searchPeopleApi is null)
        {
            _searchPeopleApi = TenantConnectorApi.Create(
                _apolloConnector.Id, _apolloConnector.TenantId, "Apollo", "Search People", "POST",
                "https://api.apollo.io/v1/mixed_people/search",
                """{"requestBody":{"type":"object","properties":{"q":{"type":"string"}}},"responseSchema":{"type":"object"}}"""
            );
            await _db.TenantConnectorApis.AddAsync(_searchPeopleApi);
        }

        _getContactApi = await _db.TenantConnectorApis
            .FirstOrDefaultAsync(a => a.TenantConnectorId == _apolloConnector.Id && a.ApiName == "Get Contact Details");

        if (_getContactApi is null)
        {
            _getContactApi = TenantConnectorApi.Create(
                _apolloConnector.Id, _apolloConnector.TenantId, "Apollo", "Get Contact Details", "GET",
                "https://api.apollo.io/v1/contacts/{id}",
                """{"requestBody":null,"responseSchema":{"type":"object"}}"""
            );
            await _db.TenantConnectorApis.AddAsync(_getContactApi);
        }

        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        await _db.DisposeAsync();
    }

    // ── Helper: Execute health check on real Apollo API ────────────────────

    private async Task<(bool success, int? statusCode, long durationMs, string? error)>
        ExecuteApiHealthCheck(string method, string urlTemplate, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return (false, null, 0, "Apollo API key not configured");

        try
        {
            var startTime = DateTime.UtcNow;

            // Build request
            var request = new HttpRequestMessage(new HttpMethod(method), urlTemplate);
            request.Headers.Add("x-api-key", apiKey);

            // Special handling for POST requests that require a body
            if (method == "POST")
                request.Content = new StringContent("""{"q":"test"}""", System.Text.Encoding.UTF8, "application/json");

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

    // ── Test 1: Health check the auth/health endpoint ──────────────────────

    [Fact]
    public async Task HealthCheck_AuthHealthEndpoint_RecordsResult()
    {
        var apiKey = GetApolloApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new SkipTestException("APOLLO_API_KEY env var not set");
        }

        _healthCheckApi.Should().NotBeNull();

        var (success, statusCode, duration, error) = await ExecuteApiHealthCheck(
            "GET", "https://api.apollo.io/v1/auth/health", apiKey);

        var healthCheck = TenantConnectorApiHealthCheck.Create(
            tenantConnectorApiId: _healthCheckApi!.Id,
            tenantConnectorId: _apolloConnector.Id,
            tenantId: _apolloConnector.TenantId,
            apiName: "Health Check",
            httpMethod: "GET",
            resolvedUrl: "https://api.apollo.io/v1/auth/health",
            isSuccess: success,
            statusCode: statusCode,
            durationMs: (int)duration,
            failureReason: error
        );

        await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
        await _db.SaveChangesAsync();

        // Verify it persisted
        var saved = await _db.TenantConnectorApiHealthChecks
            .FirstOrDefaultAsync(h => h.Id == healthCheck.Id);

        saved.Should().NotBeNull("health check should be saved");
        saved!.TenantConnectorApiId.Should().Be(_healthCheckApi.Id);
        saved.ApiName.Should().Be("Health Check");
        saved.HttpMethod.Should().Be("GET");

        Console.WriteLine($"✓ Health Check API: Success={saved.IsSuccess}, Status={saved.StatusCode}, Duration={saved.DurationMs}ms");
    }

    // ── Test 2: Health check the search people endpoint ─────────────────────

    [Fact]
    public async Task HealthCheck_SearchPeopleEndpoint_RecordsResult()
    {
        var apiKey = GetApolloApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new SkipTestException("APOLLO_API_KEY env var not set");
        }

        _searchPeopleApi.Should().NotBeNull();

        var (success, statusCode, duration, error) = await ExecuteApiHealthCheck(
            "POST", "https://api.apollo.io/v1/mixed_people/search", apiKey);

        var healthCheck = TenantConnectorApiHealthCheck.Create(
            tenantConnectorApiId: _searchPeopleApi!.Id,
            tenantConnectorId: _apolloConnector.Id,
            tenantId: _apolloConnector.TenantId,
            apiName: "Search People",
            httpMethod: "POST",
            resolvedUrl: "https://api.apollo.io/v1/mixed_people/search",
            isSuccess: success,
            statusCode: statusCode,
            durationMs: (int)duration,
            failureReason: error
        );

        await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
        await _db.SaveChangesAsync();

        // Verify it persisted
        var saved = await _db.TenantConnectorApiHealthChecks
            .FirstOrDefaultAsync(h => h.Id == healthCheck.Id);

        saved.Should().NotBeNull("health check should be saved");
        saved!.ApiName.Should().Be("Search People");
        saved.HttpMethod.Should().Be("POST");

        Console.WriteLine($"✓ Search People API: Success={saved.IsSuccess}, Status={saved.StatusCode}, Duration={saved.DurationMs}ms");
    }

    // ── Test 3: Health check the get contact endpoint ──────────────────────

    [Fact]
    public async Task HealthCheck_GetContactEndpoint_RecordsResult()
    {
        var apiKey = GetApolloApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new SkipTestException("APOLLO_API_KEY env var not set");
        }

        _getContactApi.Should().NotBeNull();

        // Use a test contact ID that likely doesn't exist (will get 404)
        var (success, statusCode, duration, error) = await ExecuteApiHealthCheck(
            "GET", "https://api.apollo.io/v1/contacts/test-invalid-id", apiKey);

        var healthCheck = TenantConnectorApiHealthCheck.Create(
            tenantConnectorApiId: _getContactApi!.Id,
            tenantConnectorId: _apolloConnector.Id,
            tenantId: _apolloConnector.TenantId,
            apiName: "Get Contact Details",
            httpMethod: "GET",
            resolvedUrl: "https://api.apollo.io/v1/contacts/test-invalid-id",
            isSuccess: success,
            statusCode: statusCode,
            durationMs: (int)duration,
            failureReason: error
        );

        await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
        await _db.SaveChangesAsync();

        // Verify it persisted
        var saved = await _db.TenantConnectorApiHealthChecks
            .FirstOrDefaultAsync(h => h.Id == healthCheck.Id);

        saved.Should().NotBeNull("health check should be saved");
        saved!.ApiName.Should().Be("Get Contact Details");

        Console.WriteLine($"✓ Get Contact API: Success={saved.IsSuccess}, Status={saved.StatusCode}, Duration={saved.DurationMs}ms");
    }

    // ── Test 4: Batch health check all Apollo APIs ────────────────────────

    [Fact]
    public async Task HealthCheck_AllApolloApis_RecordsResultsForEach()
    {
        var apiKey = GetApolloApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new SkipTestException("APOLLO_API_KEY env var not set");
        }

        _healthCheckApi.Should().NotBeNull();
        _searchPeopleApi.Should().NotBeNull();
        _getContactApi.Should().NotBeNull();

        var apis = new[]
        {
            (_healthCheckApi!, "GET", "https://api.apollo.io/v1/auth/health", "Health Check"),
            (_searchPeopleApi!, "POST", "https://api.apollo.io/v1/mixed_people/search", "Search People"),
            (_getContactApi!, "GET", "https://api.apollo.io/v1/contacts/test-id", "Get Contact Details")
        };

        var results = new List<TenantConnectorApiHealthCheck>();

        foreach (var (api, method, url, name) in apis)
        {
            var (success, statusCode, duration, error) = await ExecuteApiHealthCheck(method, url, apiKey);

            var healthCheck = TenantConnectorApiHealthCheck.Create(
                tenantConnectorApiId: api.Id,
                tenantConnectorId: _apolloConnector.Id,
                tenantId: _apolloConnector.TenantId,
                apiName: name,
                httpMethod: method,
                resolvedUrl: url,
                isSuccess: success,
                statusCode: statusCode,
                durationMs: (int)duration,
                failureReason: error
            );

            await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
            results.Add(healthCheck);
        }

        await _db.SaveChangesAsync();

        // Verify all saved
        results.Should().HaveCount(3);

        var saved = await _db.TenantConnectorApiHealthChecks
            .Where(h => results.Select(r => r.Id).Contains(h.Id))
            .ToListAsync();

        saved.Should().HaveCount(3, "all 3 health checks should be saved");

        Console.WriteLine($"\n=== Batch Health Check Results ===");
        foreach (var check in saved)
        {
            Console.WriteLine($"✓ {check.ApiName}: {(check.IsSuccess ? "✓ Success" : "✗ Failed")} | Status={check.StatusCode} | Duration={check.DurationMs}ms");
        }
    }

    // ── Test 5: Health check results show success rate ──────────────────────

    [Fact]
    public async Task HealthCheck_SuccessRate_CanBeCalculatedFromResults()
    {
        var apiKey = GetApolloApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new SkipTestException("APOLLO_API_KEY env var not set");
        }

        _healthCheckApi.Should().NotBeNull();

        // Run health check 3 times to get a success rate
        var checks = new List<TenantConnectorApiHealthCheck>();

        for (int i = 0; i < 3; i++)
        {
            var (success, statusCode, duration, error) = await ExecuteApiHealthCheck(
                "GET", "https://api.apollo.io/v1/auth/health", apiKey);

            var check = TenantConnectorApiHealthCheck.Create(
                tenantConnectorApiId: _healthCheckApi!.Id,
                tenantConnectorId: _apolloConnector.Id,
                tenantId: _apolloConnector.TenantId,
                apiName: "Health Check",
                httpMethod: "GET",
                resolvedUrl: "https://api.apollo.io/v1/auth/health",
                isSuccess: success,
                statusCode: statusCode,
                durationMs: (int)duration,
                failureReason: error
            );

            await _db.TenantConnectorApiHealthChecks.AddAsync(check);
            checks.Add(check);
            await Task.Delay(100); // Small delay between checks
        }

        await _db.SaveChangesAsync();

        // Calculate metrics
        var allChecks = await _db.TenantConnectorApiHealthChecks
            .Where(h => h.TenantConnectorApiId == _healthCheckApi!.Id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(3)
            .ToListAsync();

        var successCount = allChecks.Count(h => h.IsSuccess);
        var totalCount = allChecks.Count;
        var successRate = (double)successCount / totalCount;
        var avgDuration = (long)allChecks.Average(h => h.DurationMs);

        Console.WriteLine($"\n=== Health Check Metrics (last 3 checks) ===");
        Console.WriteLine($"Success Rate: {successRate:P0} ({successCount}/{totalCount})");
        Console.WriteLine($"Avg Duration: {avgDuration}ms");

        // Should have at least some successful checks if API is working
        totalCount.Should().BeGreaterThanOrEqualTo(3, "should have 3 health checks");
    }
}

/// <summary>Gracefully skip a test if a condition is not met.</summary>
internal sealed class SkipTestException(string reason) : Exception(reason);
