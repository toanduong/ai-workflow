using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using Npgsql;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Integration tests for manually generating Apollo APIs and persisting them to Azure PostgreSQL.
/// No Claude API calls — tests generate realistic Apollo API definitions and verify DB persistence.
///
/// Requirements:
///   PGHOST, PGUSER, PGPASSWORD, PGPORT, PGDATABASE — Azure DB credentials
///
/// NO CLEANUP — rows are left in the DB so results can be inspected.
///
/// Run with:
///   PGHOST=workflowai.postgres.database.azure.com \
///   PGUSER=postgres \
///   PGPASSWORD='Gotik$$789' \
///   PGPORT=5432 \
///   PGDATABASE=postgres \
///   dotnet test tests/WorkflowAI.Infrastructure.IntegrationTests \
///     --filter "FullyQualifiedName~GenerateApolloApisAzureDbTests"
/// </summary>
[Trait("Category", "AzureDb")]
public class GenerateApolloApisAzureDbTests : IAsyncLifetime
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

    public async Task InitializeAsync()
    {
        _db = CreateDb();

        // Find or create Apollo connector
        var existing = await _db.TenantConnectors
            .FirstOrDefaultAsync(c => c.ConnectorName == "Apollo");

        if (existing is not null)
        {
            _apolloConnector = existing;
        }
        else
        {
            _apolloConnector = TenantConnector.Create(TenantId.New(), "Apollo");
            _apolloConnector.SetMetadata(
                """{"authType":"APIKey","requiredFields":["api_key"],"endpoints":{"searchPeople":{"method":"POST","path":"/v1/mixed_people/search"},"matchPerson":{"method":"POST","path":"/v1/people/match"},"searchOrgs":{"method":"POST","path":"/v1/mixed_companies/search"}},"baseUrl":"https://api.apollo.io","testEndpoint":{"method":"GET","path":"/v1/auth/health"}}""",
                """{"description":"Apollo.io is a sales intelligence platform with 275M+ contacts","docsUrl":"https://apolloio.github.io/apollo-api-docs/","capabilities":["contact_search","company_search","lead_enrichment"],"rateLimits":"50 requests/minute","webhookSupport":true}"""
            );
            _apolloConnector.Activate();
            await _db.TenantConnectors.AddAsync(_apolloConnector);
            await _db.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync()
    {
        // NO CLEANUP — rows left in Azure DB for inspection
        await _db.DisposeAsync();
    }

    // ── Test 1: Search people API exists in DB ───────────────────────────────

    [Fact]
    public async Task ApolloApi_SearchPeople_ExistsInDb()
    {
        // Insert if not already present
        var existing = await _db.TenantConnectorApis
            .FirstOrDefaultAsync(a => a.TenantConnectorId == _apolloConnector.Id && a.ApiName == "Search People");

        if (existing is null)
        {
            var api = TenantConnectorApi.Create(
                _apolloConnector.Id, _apolloConnector.TenantId, "Apollo", "Search People", "POST",
                "https://api.apollo.io/v1/mixed_people/search",
                """{"requestBody":{"type":"object","properties":{"q":{"type":"string"}}},"responseSchema":{"type":"object"}}"""
            );
            await _db.TenantConnectorApis.AddAsync(api);
            await _db.SaveChangesAsync();
        }

        var saved = await _db.TenantConnectorApis
            .FirstOrDefaultAsync(a => a.TenantConnectorId == _apolloConnector.Id && a.ApiName == "Search People");

        saved.Should().NotBeNull("Search People API should exist in DB");
        saved!.HttpMethod.Should().Be("POST");
        saved.UrlTemplate.Should().Be("https://api.apollo.io/v1/mixed_people/search");
    }

    // ── Test 2: Get contact details API exists in DB ──────────────────────────

    [Fact]
    public async Task ApolloApi_GetContact_ExistsInDb()
    {
        // Insert if not already present
        var existing = await _db.TenantConnectorApis
            .FirstOrDefaultAsync(a => a.TenantConnectorId == _apolloConnector.Id && a.ApiName == "Get Contact Details");

        if (existing is null)
        {
            var api = TenantConnectorApi.Create(
                _apolloConnector.Id, _apolloConnector.TenantId, "Apollo", "Get Contact Details", "GET",
                "https://api.apollo.io/v1/contacts/{id}",
                """{"requestBody":null,"responseSchema":{"type":"object","properties":{"id":{"type":"string"}}}}"""
            );
            await _db.TenantConnectorApis.AddAsync(api);
            await _db.SaveChangesAsync();
        }

        var saved = await _db.TenantConnectorApis
            .FirstOrDefaultAsync(a => a.TenantConnectorId == _apolloConnector.Id && a.ApiName == "Get Contact Details");

        saved.Should().NotBeNull("Get Contact Details API should exist in DB");
        saved!.HttpMethod.Should().Be("GET");
        saved.UrlTemplate.Should().Be("https://api.apollo.io/v1/contacts/{id}");
    }

    // ── Test 3: Search organizations API exists in DB ──────────────────────────

    [Fact]
    public async Task ApolloApi_SearchOrgs_ExistsInDb()
    {
        // Insert if not already present
        var existing = await _db.TenantConnectorApis
            .FirstOrDefaultAsync(a => a.TenantConnectorId == _apolloConnector.Id && a.ApiName == "Search Organizations");

        if (existing is null)
        {
            var api = TenantConnectorApi.Create(
                _apolloConnector.Id, _apolloConnector.TenantId, "Apollo", "Search Organizations", "POST",
                "https://api.apollo.io/v1/mixed_companies/search",
                """{"requestBody":{"type":"object","properties":{"q":{"type":"string"}}},"responseSchema":{"type":"object"}}"""
            );
            await _db.TenantConnectorApis.AddAsync(api);
            await _db.SaveChangesAsync();
        }

        var saved = await _db.TenantConnectorApis
            .FirstOrDefaultAsync(a => a.TenantConnectorId == _apolloConnector.Id && a.ApiName == "Search Organizations");

        saved.Should().NotBeNull("Search Organizations API should exist in DB");
        saved!.HttpMethod.Should().Be("POST");
        saved.UrlTemplate.Should().Be("https://api.apollo.io/v1/mixed_companies/search");
    }

    // ── Test 4: All 3 APIs are retrievable ───────────────────────────────────

    [Fact]
    public async Task AllApolloApis_AreRetrievableFromDb()
    {
        // Get existing Apollo connector APIs (may already exist from previous tests)
        var existing = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _apolloConnector.Id)
            .Select(a => a.ApiName)
            .ToListAsync();

        // Insert APIs that don't already exist
        var apis = new[]
        {
            ("Search People", "POST", "https://api.apollo.io/v1/mixed_people/search"),
            ("Get Contact Details", "GET", "https://api.apollo.io/v1/contacts/{id}"),
            ("Search Organizations", "POST", "https://api.apollo.io/v1/mixed_companies/search")
        };

        foreach (var (name, method, url) in apis)
        {
            if (!existing.Contains(name))
            {
                var api = TenantConnectorApi.Create(_apolloConnector.Id, _apolloConnector.TenantId, "Apollo", name, method, url,
                    """{"requestBody":{"type":"object"},"responseSchema":{"type":"object"}}""");
                await _db.TenantConnectorApis.AddAsync(api);
            }
        }

        await _db.SaveChangesAsync();

        // Retrieve and verify all 3 exist
        var retrieved = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _apolloConnector.Id)
            .ToListAsync();

        retrieved.Should().HaveCountGreaterThanOrEqualTo(3, "all 3 Apollo APIs should be in DB");

        var apiNames = retrieved.Select(a => a.ApiName).ToList();
        apiNames.Should().Contain("Search People");
        apiNames.Should().Contain("Get Contact Details");
        apiNames.Should().Contain("Search Organizations");
    }

    // ── Test 5: All Apollo APIs have valid JSON metadata ───────────────────────

    [Fact]
    public async Task AllApolloApis_HaveValidJsonMetadata()
    {
        // Query all Apollo APIs from DB
        var saved = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _apolloConnector.Id)
            .ToListAsync();

        saved.Should().NotBeEmpty("there should be at least one Apollo API in DB");

        foreach (var api in saved)
        {
            // Each metadata string should be parseable as JSON
            var act = () => System.Text.Json.JsonDocument.Parse(api.Metadata);
            act.Should().NotThrow($"API {api.ApiName} metadata must be valid JSON");
        }
    }
}
