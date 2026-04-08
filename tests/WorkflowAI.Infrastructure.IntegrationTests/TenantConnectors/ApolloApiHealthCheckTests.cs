using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using Npgsql;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Integration tests for Apollo API health checks.
/// Verifies that TenantConnectorApiHealthCheck rows are created when testing Apollo API endpoints.
/// Tests both successful and failed health checks.
///
/// Requirements:
///   PGHOST, PGUSER, PGPASSWORD, PGPORT, PGDATABASE — Azure DB credentials
///
/// NO CLEANUP — health check audit logs left in DB for inspection.
///
/// Run with:
///   PGHOST=workflowai.postgres.database.azure.com \
///   PGUSER=postgres \
///   PGPASSWORD='Gotik$$789' \
///   PGPORT=5432 \
///   PGDATABASE=postgres \
///   dotnet test tests/WorkflowAI.Infrastructure.IntegrationTests \
///     --filter "FullyQualifiedName~ApolloApiHealthCheckTests"
/// </summary>
[Trait("Category", "AzureDb")]
public class ApolloApiHealthCheckTests : IAsyncLifetime
{
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

        var options = new DbContextOptionsBuilder<WorkflowAIDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;
        return new WorkflowAIDbContext(options);
    }

    private WorkflowAIDbContext _db = null!;
    private TenantConnector _apolloConnector = null!;
    private TenantConnectorApi? _searchPeopleApi = null;
    private TenantConnectorApi? _getContactApi = null;

    public async Task InitializeAsync()
    {
        _db = CreateDb();

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
                """{"description":"Apollo.io sales intelligence platform","docsUrl":"https://apolloio.github.io/apollo-api-docs/"}"""
            );
            _apolloConnector.Activate();
            await _db.TenantConnectors.AddAsync(_apolloConnector);
            await _db.SaveChangesAsync();
        }

        // Get or create Apollo APIs for health checking
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
        // NO CLEANUP — health check audit logs left in DB for inspection
        await _db.DisposeAsync();
    }

    // ── Test 1: Record successful health check for Search People API ────────

    [Fact]
    public async Task HealthCheck_SearchPeopleApi_RecordsSuccessful()
    {
        _searchPeopleApi.Should().NotBeNull();

        var healthCheck = TenantConnectorApiHealthCheck.Create(
            tenantConnectorApiId: _searchPeopleApi!.Id,
            tenantConnectorId: _apolloConnector.Id,
            tenantId: _apolloConnector.TenantId,
            apiName: "Search People",
            httpMethod: "POST",
            resolvedUrl: "https://api.apollo.io/v1/mixed_people/search",
            isSuccess: true,
            statusCode: 200,
            durationMs: 245,
            failureReason: null
        );

        await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
        await _db.SaveChangesAsync();

        // Verify it persisted
        var saved = await _db.TenantConnectorApiHealthChecks
            .FirstOrDefaultAsync(h => h.Id == healthCheck.Id);

        saved.Should().NotBeNull("health check should exist in DB");
        saved!.IsSuccess.Should().BeTrue();
        saved.StatusCode.Should().Be(200);
        saved.DurationMs.Should().Be(245);
        saved.FailureReason.Should().BeNull();
    }

    // ── Test 2: Record failed health check with error ──────────────────────

    [Fact]
    public async Task HealthCheck_GetContactApi_RecordsFailed()
    {
        _getContactApi.Should().NotBeNull();

        var healthCheck = TenantConnectorApiHealthCheck.Create(
            tenantConnectorApiId: _getContactApi!.Id,
            tenantConnectorId: _apolloConnector.Id,
            tenantId: _apolloConnector.TenantId,
            apiName: "Get Contact Details",
            httpMethod: "GET",
            resolvedUrl: "https://api.apollo.io/v1/contacts/invalid-id",
            isSuccess: false,
            statusCode: 404,
            durationMs: 89,
            failureReason: "Contact not found"
        );

        await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
        await _db.SaveChangesAsync();

        // Verify it persisted
        var saved = await _db.TenantConnectorApiHealthChecks
            .FirstOrDefaultAsync(h => h.Id == healthCheck.Id);

        saved.Should().NotBeNull();
        saved!.IsSuccess.Should().BeFalse();
        saved.StatusCode.Should().Be(404);
        saved.FailureReason.Should().Be("Contact not found");
    }

    // ── Test 3: Record timeout/network error (no status code) ─────────────

    [Fact]
    public async Task HealthCheck_TimeoutError_RecordsWithoutStatusCode()
    {
        _searchPeopleApi.Should().NotBeNull();

        var healthCheck = TenantConnectorApiHealthCheck.Create(
            tenantConnectorApiId: _searchPeopleApi!.Id,
            tenantConnectorId: _apolloConnector.Id,
            tenantId: _apolloConnector.TenantId,
            apiName: "Search People",
            httpMethod: "POST",
            resolvedUrl: "https://api.apollo.io/v1/mixed_people/search",
            isSuccess: false,
            statusCode: null,  // No HTTP response due to timeout
            durationMs: 30000,
            failureReason: "Request timeout after 30 seconds"
        );

        await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
        await _db.SaveChangesAsync();

        // Verify it persisted
        var saved = await _db.TenantConnectorApiHealthChecks
            .FirstOrDefaultAsync(h => h.Id == healthCheck.Id);

        saved.Should().NotBeNull();
        saved!.IsSuccess.Should().BeFalse();
        saved.StatusCode.Should().BeNull("timeout has no HTTP status code");
        saved.DurationMs.Should().Be(30000);
    }

    // ── Test 4: Retrieve all health checks for Apollo connector ────────────

    [Fact]
    public async Task HealthChecks_ForConnector_RetrievableFromDb()
    {
        _searchPeopleApi.Should().NotBeNull();
        _getContactApi.Should().NotBeNull();

        // Insert multiple health checks
        var checks = new[]
        {
            TenantConnectorApiHealthCheck.Create(
                _searchPeopleApi!.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Search People", "POST", "https://api.apollo.io/v1/mixed_people/search",
                true, 200, 150, null),
            TenantConnectorApiHealthCheck.Create(
                _searchPeopleApi.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Search People", "POST", "https://api.apollo.io/v1/mixed_people/search",
                true, 200, 162, null),
            TenantConnectorApiHealthCheck.Create(
                _getContactApi!.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Get Contact Details", "GET", "https://api.apollo.io/v1/contacts/{id}",
                false, 401, 45, "Unauthorized: Invalid API key")
        };

        await _db.TenantConnectorApiHealthChecks.AddRangeAsync(checks);
        await _db.SaveChangesAsync();

        // Retrieve all checks for connector
        var retrieved = await _db.TenantConnectorApiHealthChecks
            .Where(h => h.TenantConnectorId == _apolloConnector.Id)
            .ToListAsync();

        retrieved.Should().HaveCountGreaterThanOrEqualTo(3, "at least 3 health checks should be in DB");
    }

    // ── Test 5: Health check audit trail (recent checks for API) ──────────

    [Fact]
    public async Task HealthChecks_AuditTrail_ShowsApiCheckHistory()
    {
        _searchPeopleApi.Should().NotBeNull();

        // Insert sequential health checks simulating multiple test runs
        var now = DateTime.UtcNow;
        var checks = new[]
        {
            TenantConnectorApiHealthCheck.Create(
                _searchPeopleApi!.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Search People", "POST", "https://api.apollo.io/v1/mixed_people/search",
                true, 200, 240, null),
            TenantConnectorApiHealthCheck.Create(
                _searchPeopleApi.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Search People", "POST", "https://api.apollo.io/v1/mixed_people/search",
                true, 200, 235, null),
            TenantConnectorApiHealthCheck.Create(
                _searchPeopleApi.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Search People", "POST", "https://api.apollo.io/v1/mixed_people/search",
                false, 429, 100, "Rate limit exceeded")
        };

        await _db.TenantConnectorApiHealthChecks.AddRangeAsync(checks);
        await _db.SaveChangesAsync();

        // Query latest health checks for Search People API
        var history = await _db.TenantConnectorApiHealthChecks
            .Where(h => h.TenantConnectorApiId == _searchPeopleApi!.Id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(5)
            .ToListAsync();

        history.Should().HaveCountGreaterThanOrEqualTo(3, "at least 3 checks in history");

        // Verify audit trail shows 2 successes, 1 failure
        var successCount = history.Count(h => h.IsSuccess);
        var failureCount = history.Count(h => !h.IsSuccess);
        successCount.Should().BeGreaterThan(0, "should have successful checks");
        failureCount.Should().BeGreaterThan(0, "should have failed checks");
    }

    // ── Test 6: Health check with long error message (truncated) ──────────

    [Fact]
    public async Task HealthCheck_LongErrorMessage_IsTruncatedTo1000Chars()
    {
        _getContactApi.Should().NotBeNull();

        var longError = new string('x', 2000);

        var healthCheck = TenantConnectorApiHealthCheck.Create(
            tenantConnectorApiId: _getContactApi!.Id,
            tenantConnectorId: _apolloConnector.Id,
            tenantId: _apolloConnector.TenantId,
            apiName: "Get Contact Details",
            httpMethod: "GET",
            resolvedUrl: "https://api.apollo.io/v1/contacts/test",
            isSuccess: false,
            statusCode: 500,
            durationMs: 1500,
            failureReason: longError
        );

        await _db.TenantConnectorApiHealthChecks.AddAsync(healthCheck);
        await _db.SaveChangesAsync();

        // Verify error is truncated
        var saved = await _db.TenantConnectorApiHealthChecks
            .FirstOrDefaultAsync(h => h.Id == healthCheck.Id);

        saved.Should().NotBeNull();
        saved!.FailureReason.Should().NotBeNull();
        saved.FailureReason!.Length.Should().BeLessThanOrEqualTo(1000, "error should be truncated to 1000 chars");
    }

    // ── Test 7: Health check metrics (success rate, avg duration) ─────────

    [Fact]
    public async Task HealthChecks_Metrics_CalculateSuccessRateAndAvgDuration()
    {
        _searchPeopleApi.Should().NotBeNull();

        // Insert health checks with varying outcomes
        var checks = new[]
        {
            TenantConnectorApiHealthCheck.Create(_searchPeopleApi!.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Search People", "POST", "https://api.apollo.io/v1/mixed_people/search", true, 200, 150, null),
            TenantConnectorApiHealthCheck.Create(_searchPeopleApi.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Search People", "POST", "https://api.apollo.io/v1/mixed_people/search", true, 200, 160, null),
            TenantConnectorApiHealthCheck.Create(_searchPeopleApi.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Search People", "POST", "https://api.apollo.io/v1/mixed_people/search", true, 200, 170, null),
            TenantConnectorApiHealthCheck.Create(_searchPeopleApi.Id, _apolloConnector.Id, _apolloConnector.TenantId,
                "Search People", "POST", "https://api.apollo.io/v1/mixed_people/search", false, 500, 5000, "Server error")
        };

        await _db.TenantConnectorApiHealthChecks.AddRangeAsync(checks);
        await _db.SaveChangesAsync();

        // Calculate metrics
        var allChecks = await _db.TenantConnectorApiHealthChecks
            .Where(h => h.TenantConnectorApiId == _searchPeopleApi!.Id)
            .ToListAsync();

        var successCount = allChecks.Count(h => h.IsSuccess);
        var totalCount = allChecks.Count;
        var successRate = (double)successCount / totalCount;
        var avgDuration = (long)allChecks.Average(h => h.DurationMs);

        successRate.Should().BeGreaterThan(0.5, "success rate should be > 50% (3/4 passed)");
        avgDuration.Should().BeGreaterThan(0, "average duration should be calculable");
    }
}
