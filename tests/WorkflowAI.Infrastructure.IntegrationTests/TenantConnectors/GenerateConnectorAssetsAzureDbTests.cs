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
/// End-to-end integration tests for GenerateConnectorAssets against Azure PostgreSQL:
///   - Real Claude API generates the API routes and workflow templates
///   - Real Azure Postgres DB stores them (SqlTenantConnectorRepository + SqlTemplateRepository)
///   - Tests read rows back from the DB to confirm persistence
///
/// Requirements:
///   ANTHROPIC_API_KEY              — real Claude API key
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
///   ANTHROPIC_API_KEY=sk-ant-... \
///   dotnet test tests/WorkflowAI.Infrastructure.IntegrationTests \
///     --filter "FullyQualifiedName~GenerateConnectorAssetsAzureDbTests"
/// </summary>
[Trait("Category", "AzureDb")]
public class GenerateConnectorAssetsAzureDbTests : IAsyncLifetime
{
    private static readonly string AnthropicApiKey =
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set.");

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

    private WorkflowAIDbContext _db = null!;
    private SqlTenantConnectorRepository _connectorRepository = null!;
    private SqlTemplateRepository _templateRepository = null!;
    private GenerateConnectorAssetsCommandHandler _handler = null!;
    private TenantConnector _apolloConnector = null!;

    public async Task InitializeAsync()
    {
        _db = CreateDb();
        _connectorRepository = new SqlTenantConnectorRepository(_db);
        _templateRepository = new SqlTemplateRepository(_db);

        // Find or create Apollo connector in Active state
        var existingApollo = await _connectorRepository.GetByTenantAndNameAsync(
            TenantId.New(), "Apollo");

        if (existingApollo is not null)
        {
            _apolloConnector = existingApollo;
        }
        else
        {
            // Create fresh Apollo connector in Active state with metadata
            _apolloConnector = TenantConnector.Create(TenantId.New(), "Apollo");
            _apolloConnector.SetMetadata(
                """{"authType":"APIKey","requiredFields":["api_key"],"endpoints":{"searchPeople":{"method":"POST","path":"/v1/mixed_people/search"},"matchPerson":{"method":"POST","path":"/v1/people/match"},"searchOrgs":{"method":"POST","path":"/v1/mixed_companies/search"}},"baseUrl":"https://api.apollo.io","testEndpoint":{"method":"GET","path":"/v1/auth/health"}}""",
                """{"description":"Apollo.io is a sales intelligence platform with 275M+ contacts","docsUrl":"https://apolloio.github.io/apollo-api-docs/","capabilities":["contact_search","company_search","lead_enrichment"],"rateLimits":"50 requests/minute","webhookSupport":true}"""
            );
            _apolloConnector.Activate();
            await _connectorRepository.AddAsync(_apolloConnector);
        }

        // Build handler with real repos and Claude service
        IChatClient chatClient = new AnthropicClient(
            apiKeys: new APIAuthentication(AnthropicApiKey)).Messages;

        var anthropicOptions = Options.Create(new AnthropicOptions
        {
            ApiKey = AnthropicApiKey,
            DefaultModel = "claude-opus-4-6"
        });

        var anthropicService = new AnthropicService(
            chatClient, anthropicOptions, NullLogger<AnthropicService>.Instance);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.IsAuthenticated.Returns(true);

        var options = Options.Create(new ConnectorAssetGenerationOptions());

        _handler = new GenerateConnectorAssetsCommandHandler(
            _connectorRepository, anthropicService, currentUser, options,
            NullLogger<GenerateConnectorAssetsCommandHandler>.Instance);
    }

    public async Task DisposeAsync()
    {
        // NO CLEANUP — rows left in Azure DB for inspection
        await _db.DisposeAsync();
    }

    // ── Test 1: command succeeds and APIs are persisted to Azure DB ────────────

    [Fact]
    public async Task GenerateConnectorAssets_Apollo_PersistsApisToAzureDb()
    {
        var result = await _handler.Handle(
            new GenerateConnectorAssetsCommand(_apolloConnector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            $"Claude should successfully generate assets for Apollo. Error: {result.Error?.Message}");

        // Query the DB for API operations we just created
        var savedApis = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _apolloConnector.Id)
            .ToListAsync();

        savedApis.Should().NotBeEmpty(
            "at least one API operation should have been saved to TenantConnectorApis");
    }

    // ── Test 2: at least 2 API operations are saved ──────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_Apollo_SavesAtLeastTwoApiOperations()
    {
        var result = await _handler.Handle(
            new GenerateConnectorAssetsCommand(_apolloConnector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var savedApis = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _apolloConnector.Id)
            .ToListAsync();

        savedApis.Should().HaveCountGreaterThanOrEqualTo(2,
            "Claude should generate at least 2 API operations for Apollo");
    }

    // ── Test 3: API operations have required fields ──────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_Apollo_ApiOperationsHaveRequiredFields()
    {
        var result = await _handler.Handle(
            new GenerateConnectorAssetsCommand(_apolloConnector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var savedApis = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _apolloConnector.Id)
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

    // ── Test 4: workflow templates are also persisted to Azure DB ──────────────

    [Fact]
    public async Task GenerateConnectorAssets_Apollo_PersistsWorkflowTemplatesToAzureDb()
    {
        var result = await _handler.Handle(
            new GenerateConnectorAssetsCommand(_apolloConnector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var workflowCategory = $"{_apolloConnector.ConnectorType}/Workflow";
        var savedWorkflows = await _db.Templates
            .Where(t => t.Category == workflowCategory)
            .ToListAsync();

        savedWorkflows.Should().HaveCountGreaterThanOrEqualTo(2,
            "Claude should generate at least 2 workflow templates");
    }

    // ── Test 5: API and template counts match the result ───────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_Apollo_ResultCountsMatchDatabaseRows()
    {
        var result = await _handler.Handle(
            new GenerateConnectorAssetsCommand(_apolloConnector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var dbApiCount = await _db.TenantConnectorApis
            .Where(a => a.TenantConnectorId == _apolloConnector.Id)
            .CountAsync();

        var resultApiRoutes = JsonDocument.Parse(result.Value!.ApiRoutes)
            .RootElement.GetArrayLength();

        resultApiRoutes.Should().Be(dbApiCount,
            "the number of API routes in the result should match the number of rows saved in DB");
    }
}
