using Anthropic.SDK;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using WorkflowAI.Application.TenantConnectors.Commands.ProvisionTenantConnector;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.AI;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// End-to-end test: Claude generates metadata for the Apollo connector
/// and the row is written to Azure PostgreSQL.
///
/// NO CLEANUP — rows are left in the DB so results can be inspected.
///
/// Reads config from src/WorkflowAI.Functions/local.settings.json:
///   Anthropic__ApiKey  — Claude API key
///
/// Azure DB credentials are read from PGHOST/PGUSER/PGPASSWORD/PGPORT/PGDATABASE
/// env vars (set these before running):
///   export PGHOST=workflowai.postgres.database.azure.com
///   export PGUSER=postgres
///   export PGPASSWORD='Gotik$$789'
///   export PGPORT=5432
///   export PGDATABASE=postgres
///
/// Run with:
///   dotnet test tests/WorkflowAI.Infrastructure.IntegrationTests \
///     --filter "FullyQualifiedName~ProvisionApolloAzureDbTests"
/// </summary>
[Trait("Category", "AzureDb")]
public class ProvisionApolloAzureDbTests
{
    // Read Anthropic API key from local.settings.json (same file the Functions project uses)
    private static readonly string AnthropicApiKey = ReadLocalSetting("Anthropic__ApiKey")
        ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? throw new InvalidOperationException(
            "Anthropic__ApiKey not found in local.settings.json and ANTHROPIC_API_KEY env var is not set.");

    private static string? ReadLocalSetting(string key)
    {
        try
        {
            // local.settings.json is copied next to the test binary via the .csproj Content item
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "local.settings.json");
            if (!File.Exists(settingsPath)) return null;

            var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            if (doc.RootElement.TryGetProperty("Values", out var values) &&
                values.TryGetProperty(key, out var val))
                return val.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    // Azure Postgres — reads from standard PG* env vars (set before running)
    // Uses NpgsqlConnectionStringBuilder so special chars in the password
    // ($$, {, }) are never misinterpreted by the connection string parser
    private static WorkflowAIDbContext CreateDb()
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host     = Environment.GetEnvironmentVariable("PGHOST")     ?? "workflowai.postgres.database.azure.com",
            Port     = int.TryParse(Environment.GetEnvironmentVariable("PGPORT"), out var p) ? p : 5432,
            Database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "postgres",
            Username = Environment.GetEnvironmentVariable("PGUSER")     ?? "postgres",
            Password = Environment.GetEnvironmentVariable("PGPASSWORD")
                       ?? throw new InvalidOperationException("PGPASSWORD env var is not set."),
            SslMode  = Npgsql.SslMode.Require
        };

        var options = new DbContextOptionsBuilder<WorkflowAIDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;
        return new WorkflowAIDbContext(options);
    }

    private static ProvisionTenantConnectorCommandHandler CreateHandler()
    {
        IChatClient chatClient = new AnthropicClient(
            apiKeys: new APIAuthentication(AnthropicApiKey)).Messages;

        var anthropicOptions = Options.Create(new AnthropicOptions
        {
            ApiKey = AnthropicApiKey,
            DefaultModel = "claude-opus-4-6"
        });

        var anthropicService = new AnthropicService(
            chatClient, anthropicOptions, NullLogger<AnthropicService>.Instance);

        var db = CreateDb();
        var repository = new SqlTenantConnectorRepository(db);

        return new ProvisionTenantConnectorCommandHandler(repository, anthropicService);
    }

    // ── Test 1: command succeeds ──────────────────────────────────────────────

    [Fact]
    public async Task Provision_Apollo_ReturnsSuccess()
    {
        var handler = CreateHandler();
        var tenantId = Guid.NewGuid();

        var result = await handler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, "Apollo"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            $"Claude should generate metadata for Apollo. Error: {result.Error?.Message}");

        result.Value!.ConnectorName.Should().Be("Apollo");
        result.Value.Metadata.Should().NotBeNullOrWhiteSpace();
        result.Value.Info.Should().NotBeNullOrWhiteSpace();
    }

    // ── Test 2: metadata is valid JSON with required fields ───────────────────

    [Fact]
    public async Task Provision_Apollo_MetadataHasCorrectStructure()
    {
        var handler = CreateHandler();
        var tenantId = Guid.NewGuid();

        var result = await handler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, "Apollo"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var metadata = JsonDocument.Parse(result.Value!.Metadata).RootElement;

        metadata.TryGetProperty("authType", out var authType).Should().BeTrue("metadata must have authType");
        authType.GetString().Should().BeOneOf("APIKey", "OAuth2", "Basic", "Bearer");

        metadata.TryGetProperty("requiredFields", out var requiredFields).Should().BeTrue("metadata must have requiredFields");
        requiredFields.ValueKind.Should().Be(JsonValueKind.Array);
        requiredFields.GetArrayLength().Should().BeGreaterThan(0, "Apollo requires at least one credential field");

        metadata.TryGetProperty("baseUrl", out var baseUrl).Should().BeTrue("metadata must have baseUrl");
        baseUrl.GetString().Should().NotBeNullOrWhiteSpace();

        metadata.TryGetProperty("testEndpoint", out var testEndpoint).Should().BeTrue("metadata must have testEndpoint");
        testEndpoint.TryGetProperty("method", out _).Should().BeTrue("testEndpoint must have method");
        testEndpoint.TryGetProperty("path", out _).Should().BeTrue("testEndpoint must have path");
    }

    // ── Test 3: row is persisted to Azure DB ─────────────────────────────────

    [Fact]
    public async Task Provision_Apollo_RowExistsInAzureDb()
    {
        var handler = CreateHandler();
        var tenantId = Guid.NewGuid();

        var result = await handler.Handle(
            new ProvisionTenantConnectorCommand(tenantId, "Apollo"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var connectorId = TenantConnectorId.From(result.Value!.TenantConnectorId);

        // Read back using a fresh DB context
        await using var db = CreateDb();
        var saved = await db.TenantConnectors
            .FirstOrDefaultAsync(c => c.Id == connectorId);

        saved.Should().NotBeNull("row must exist in Azure Postgres after provisioning");
        saved!.ConnectorName.Should().Be("Apollo");
        saved.TenantId.Value.Should().Be(tenantId);
        saved.Status.Should().Be(TenantConnectorStatus.Pending);
        saved.Metadata.Should().NotBeNullOrWhiteSpace();
        saved.Info.Should().NotBeNullOrWhiteSpace();
    }
}
