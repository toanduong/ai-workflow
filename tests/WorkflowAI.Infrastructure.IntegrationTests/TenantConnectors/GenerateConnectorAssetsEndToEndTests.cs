using Anthropic.SDK;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;
using WorkflowAI.Domain.Templates;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.AI;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// End-to-end integration tests for GenerateConnectorAssets:
///   - Real Claude API generates the API routes and workflow templates
///   - Real Postgres DB stores them (SqlTemplateRepository)
///   - Tests read rows back from the DB to confirm persistence
///
/// Requirements:
///   ANTHROPIC_API_KEY              — real Claude API key
///   WORKFLOWAI_CONNECTION_STRING   — optional; defaults to docker-compose DB
///   CONNECTOR_NAME                 — optional; defaults to "Apollo"
///
/// Cleanup: all WorkflowTemplate rows created during the run are deleted in DisposeAsync.
/// </summary>
[Trait("Category", "Integration")]
public class GenerateConnectorAssetsEndToEndTests : IAsyncLifetime
{
    private static readonly string AnthropicApiKey =
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set.");

    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("WORKFLOWAI_CONNECTION_STRING")
        ?? "Host=localhost;Port=5433;Database=workflowai_dev;Username=postgres;Password=devpassword;Ssl Mode=Disable";

    private static readonly string ConnectorName =
        Environment.GetEnvironmentVariable("CONNECTOR_NAME") ?? "Apollo";

    private WorkflowAIDbContext _db = null!;
    private SqlTenantConnectorRepository _connectorRepository = null!;
    private GenerateConnectorAssetsCommandHandler _handler = null!;
    private TenantConnector _connector = null!;

    public Task InitializeAsync()
    {
        var dbOptions = new DbContextOptionsBuilder<WorkflowAIDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        _db = new WorkflowAIDbContext(dbOptions);
        _connectorRepository = new SqlTenantConnectorRepository(_db);

        // Build a connector in Active state — ready for asset generation
        _connector = TenantConnector.Create(TenantId.New(), ConnectorName);
        // Use minimal stub metadata so the test works for any connector name
        _connector.SetMetadata(
            """{"authType":"APIKey","requiredFields":["api_key"],"endpoints":{},"configSchema":{},"baseUrl":"https://api.example.com","testEndpoint":{"method":"GET","path":"/"}}""",
            $$$"""{"description":"{{{ConnectorName}}} integration","docsUrl":"https://example.com","capabilities":[],"rateLimits":"unknown","webhookSupport":false}""");
        _connector.Activate();

        IChatClient chatClient = new AnthropicClient(
            apiKeys: new APIAuthentication(AnthropicApiKey)).Messages;
        var anthropicOptions = Options.Create(new AnthropicOptions
        {
            ApiKey = AnthropicApiKey,
            DefaultModel = "claude-opus-4-6"
        });
        var claudeService = new AnthropicService(
            chatClient, anthropicOptions, NullLogger<AnthropicService>.Instance);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.IsAuthenticated.Returns(true);

        _handler = new GenerateConnectorAssetsCommandHandler(
            _connectorRepository, claudeService, currentUser,
            NullLogger<GenerateConnectorAssetsCommandHandler>.Instance);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // NO CLEANUP — rows left in the DB for inspection
        await _db.DisposeAsync();
    }

    // ── Test 1: command succeeds and APIs are persisted to DB ──────────────

    [Fact]
    public async Task GenerateConnectorAssets_PersistsApisToDatabase()
    {
        var result = await _handler.Handle(
            new GenerateConnectorAssetsCommand(_connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            $"Claude should generate assets for '{ConnectorName}'");

        var savedApis = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _connector.Id)
            .ToListAsync();

        savedApis.Should().NotBeEmpty("at least one API operation must have been saved");
    }

    // ── Test 2: at least 2 API operations are in DB ──────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_SavesAtLeastTwoApiOperations()
    {
        await _handler.Handle(
            new GenerateConnectorAssetsCommand(_connector.Id.Value), CancellationToken.None);

        var savedApis = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _connector.Id)
            .ToListAsync();

        savedApis.Should().HaveCountGreaterThanOrEqualTo(2,
            "Claude should generate at least 2 API operations");
    }

    // ── Test 3: API operations have valid structure ──────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_ApiOperationsHaveValidStructure()
    {
        await _handler.Handle(
            new GenerateConnectorAssetsCommand(_connector.Id.Value), CancellationToken.None);

        var savedApis = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _connector.Id)
            .ToListAsync();

        savedApis.Should().NotBeEmpty();

        foreach (var api in savedApis)
        {
            api.ApiName.Should().NotBeNullOrWhiteSpace("each API must have a name");
            api.HttpMethod.Should().BeOneOf("GET", "POST", "PUT", "PATCH", "DELETE",
                "HTTP method must be a valid verb");
            api.UrlTemplate.Should().NotBeNullOrWhiteSpace("each API must have a URL template");
            api.Metadata.Should().NotBeNullOrWhiteSpace("each API must have metadata");

            // Metadata must be valid JSON
            var metadataDoc = JsonDocument.Parse(api.Metadata);
            metadataDoc.RootElement.ValueKind.Should().Be(JsonValueKind.Object,
                "API metadata must be valid JSON object");
        }
    }
}
