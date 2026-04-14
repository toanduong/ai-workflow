using Anthropic.SDK;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.Text.Json;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Workflows.Commands.GenerateArmTemplate;
using WorkflowAI.Infrastructure.AI;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

namespace WorkflowAI.Infrastructure.IntegrationTests.Workflows;

/// <summary>
/// End-to-end integration tests for POST /tenants/{tenantId}/workflows/generate.
///
/// Prerequisites (seed-data.sql must have been run):
///   TenantId a1b2c3d4-0000-0000-0000-000000000001 must have 3 active connectors:
///   Odoo (7 APIs), Apollo (4 APIs), Chatwoot (6 APIs).
///
/// Environment variables:
///   AI_PROVIDER                      — "Anthropic" (default) | "GitHubModels"
///   ANTHROPIC_API_KEY                — required when AI_PROVIDER=Anthropic
///   GITHUB_MODELS_TOKEN              — required when AI_PROVIDER=GitHubModels
///   AI_MODEL                         — optional, defaults to "claude-sonnet-4-5"
///   WORKFLOWAI_CONNECTION_STRING     — optional, defaults to local docker DB
/// </summary>
[Trait("Category", "Integration")]
public class GenerateArmTemplateEndToEndTests : IAsyncLifetime
{
    // TenantId from seed-data.sql
    private static readonly Guid SeedTenantId = Guid.Parse("a1b2c3d4-0000-0000-0000-000000000001");

    private static readonly string AiProvider =
        Environment.GetEnvironmentVariable("AI_PROVIDER") ?? "Anthropic";

    // Default model differs per provider: Claude for Anthropic, gpt-4o for GitHub Models
    private static readonly string AiModel =
        Environment.GetEnvironmentVariable("AI_MODEL")
        ?? (AiProvider.Equals("GitHubModels", StringComparison.OrdinalIgnoreCase) ? "gpt-4o" : "claude-sonnet-4-5");

    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("WORKFLOWAI_CONNECTION_STRING")
        ?? "Host=localhost;Port=5433;Database=workflowai_dev;Username=postgres;Password=devpassword;Ssl Mode=Disable";

    private WorkflowAIDbContext _db = null!;
    private GenerateArmTemplateFromPromptCommandHandler _handler = null!;

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<WorkflowAIDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        _db = new WorkflowAIDbContext(options);
        var repository = new SqlTenantConnectorRepository(_db);

        IChatClient chatClient = CreateChatClient();

        var anthropicOptions = Options.Create(new AnthropicOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty,
            DefaultModel = AiModel
        });

        var anthropicService = new AnthropicService(
            chatClient, anthropicOptions, NullLogger<AnthropicService>.Instance);

        var blobStorageService = new FakeBlobStorageService();

        // Create a mock configuration for credentials
        var inMemorySettings = new Dictionary<string, string>
        {
            ["Connectors:Odoo:ApiKey"] = "test-api-key",
            ["Connectors:Odoo:InstanceUrl"] = "https://test.odoo.com",
            ["Connectors:Odoo:UserLogin"] = "test@example.com"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _handler = new GenerateArmTemplateFromPromptCommandHandler(
            repository, 
            anthropicService, 
            blobStorageService,
            configuration);

        return Task.CompletedTask;
    }

    private static IChatClient CreateChatClient()
    {
        if (string.Equals(AiProvider, "GitHubModels", StringComparison.OrdinalIgnoreCase))
        {
            var token = Environment.GetEnvironmentVariable("GITHUB_MODELS_TOKEN")
                ?? throw new InvalidOperationException("GITHUB_MODELS_TOKEN environment variable is not set.");
            var endpoint = Environment.GetEnvironmentVariable("GITHUB_MODELS_ENDPOINT")
                ?? "https://models.inference.ai.azure.com";
            return new OpenAIClient(
                new ApiKeyCredential(token),
                new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
                .GetChatClient(AiModel)
                .AsIChatClient();
        }

        // Default: Anthropic SDK
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set.");
        return new AnthropicClient(apiKeys: new APIAuthentication(apiKey)).Messages;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    // ── Test 1: basic successful generation ───────────────────────────────────

    [Fact]
    public async Task Generate_ReturnsSuccessWithArmTemplateJson()
    {
        var command = new GenerateArmTemplateFromPromptCommand(
            SeedTenantId,
            "Get list of contacts from Odoo");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            $"Handler should succeed. Error: {result.Error?.Message}");

        result.Value!.WorkflowName.Should().NotBeNullOrWhiteSpace();
        result.Value.ArmTemplateJson.Should().NotBeNullOrWhiteSpace();
        result.Value.ConnectorsUsed.Should().NotBeEmpty();
        result.Value.MissingApis.Should().BeEmpty("all requested APIs should be found in seed data");
    }

    // ── Test 2: ARM template JSON is well-formed and contains expected fields ─

    [Fact]
    public async Task Generate_ArmTemplateHasCorrectStructure()
    {
        var command = new GenerateArmTemplateFromPromptCommand(
            SeedTenantId,
            "Search people in Apollo and create contacts");

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue($"Error: {result.Error?.Message}");

        var armDoc = JsonDocument.Parse(result.Value!.ArmTemplateJson);
        var root = armDoc.RootElement;

        root.TryGetProperty("$schema", out var schema).Should().BeTrue("ARM template must have $schema");
        schema.GetString().Should().Contain("deploymentTemplate", "must reference ARM deployment schema");

        root.TryGetProperty("parameters", out _).Should().BeTrue("ARM template must have parameters section");
        root.TryGetProperty("resources", out var resources).Should().BeTrue("ARM template must have resources");

        resources.ValueKind.Should().Be(JsonValueKind.Array);
        resources.GetArrayLength().Should().BeGreaterThan(0, "at least one Logic App resource must be present");

        var logicApp = resources[0];
        logicApp.GetProperty("type").GetString().Should().Be("Microsoft.Logic/workflows");
        logicApp.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ── Test 3: Logic App definition has HTTP actions ─────────────────────────

    [Fact]
    public async Task Generate_LogicAppDefinitionContainsHttpActions()
    {
        var command = new GenerateArmTemplateFromPromptCommand(
            SeedTenantId,
            "Get contacts from Chatwoot and enrich with Apollo");

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue($"Error: {result.Error?.Message}");

        var armDoc = JsonDocument.Parse(result.Value!.ArmTemplateJson);
        var definition = armDoc.RootElement
            .GetProperty("resources")[0]
            .GetProperty("properties")
            .GetProperty("definition");

        definition.TryGetProperty("actions", out var actions).Should().BeTrue(
            "Logic App definition must contain actions");

        actions.ValueKind.Should().Be(JsonValueKind.Object);
        actions.EnumerateObject().Should().NotBeEmpty("there must be at least one action step");

        // Each action must be of type Http
        foreach (var action in actions.EnumerateObject())
        {
            action.Value.GetProperty("type").GetString().Should().Be("Http",
                $"action '{action.Name}' must be of type Http");

            var inputs = action.Value.GetProperty("inputs");
            inputs.TryGetProperty("method", out var method).Should().BeTrue();
            inputs.TryGetProperty("uri", out _).Should().BeTrue();

            method.GetString().Should().BeOneOf("GET", "POST", "PUT", "PATCH", "DELETE");
        }
    }

    // ── Test 4: multi-connector workflow uses connectors from seed data ────────

    [Fact]
    public async Task Generate_MultiConnectorWorkflow_UsesMultipleConnectors()
    {
        var command = new GenerateArmTemplateFromPromptCommand(
            SeedTenantId,
            "Get contacts from Odoo, enrich them with Apollo, then create a conversation in Chatwoot for each");

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue($"Error: {result.Error?.Message}");

        result.Value!.ConnectorsUsed.Should().HaveCountGreaterThan(1,
            "a multi-connector prompt should use more than one connector");

        result.Value.ConnectorsUsed.Should().OnlyContain(
            name => new[] { "Odoo", "Apollo", "Chatwoot" }.Contains(name, StringComparer.OrdinalIgnoreCase),
            "only connectors from seed data can appear");
    }

    // ── Test 5: ARM parameters use securestring for credentials ───────────────

    [Fact]
    public async Task Generate_ArmParameters_AreSecureStrings()
    {
        var command = new GenerateArmTemplateFromPromptCommand(
            SeedTenantId,
            "List Odoo contacts");

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue($"Error: {result.Error?.Message}");

        var armDoc = JsonDocument.Parse(result.Value!.ArmTemplateJson);
        var parameters = armDoc.RootElement.GetProperty("parameters");

        parameters.EnumerateObject().Should().NotBeEmpty(
            "there must be at least one parameter for credentials");

        foreach (var param in parameters.EnumerateObject())
        {
            param.Value.GetProperty("type").GetString().Should().Be("securestring",
                $"parameter '{param.Name}' must be securestring to protect credentials");
        }
    }

    // ── Test 6: tenant with no active connectors returns NotFound ─────────────

    [Fact]
    public async Task Generate_NoActiveConnectors_ReturnsNotFoundError()
    {
        var emptyTenantId = Guid.NewGuid(); // tenant with no data
        var command = new GenerateArmTemplateFromPromptCommand(
            emptyTenantId,
            "Get contacts from Odoo");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Workflow.NoActiveConnectors");
    }

    // ── Test 7: workflowName is kebab-case ────────────────────────────────────

    [Fact]
    public async Task Generate_WorkflowName_IsKebabCase()
    {
        var command = new GenerateArmTemplateFromPromptCommand(
            SeedTenantId,
            "List sale orders from Odoo");

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue($"Error: {result.Error?.Message}");

        var name = result.Value!.WorkflowName;
        name.Should().MatchRegex(@"^[a-z][a-z0-9\-]{0,39}$",
            "workflowName must be lowercase kebab-case, max 40 chars");
    }

    // ── Test 8: Phase 1 connector selection filters to relevant connectors ────

    [Fact]
    public async Task Generate_Phase1Selection_FiltersToRelevantConnectors()
    {
        // Prompt explicitly mentions only Odoo — Phase 1 should exclude Apollo and Chatwoot
        var command = new GenerateArmTemplateFromPromptCommand(
            SeedTenantId,
            "Get all contacts from Odoo CRM");

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue($"Error: {result.Error?.Message}");

        result.Value!.ConnectorsUsed.Should().HaveCount(1,
            "an Odoo-only prompt should use exactly one connector after Phase 1 filtering");

        result.Value.ConnectorsUsed.Should().ContainSingle(
            name => name.Equals("Odoo", StringComparison.OrdinalIgnoreCase),
            "Phase 1 should have selected Odoo and excluded Apollo and Chatwoot");
    }
}

/// <summary>
/// Fake blob storage service for testing - does not actually upload files.
/// </summary>
file sealed class FakeBlobStorageService : IBlobStorageService
{
    public Task<string> UploadAsync(string containerName, string blobName, string content, CancellationToken cancellationToken = default)
    {
        // Return a fake blob URL
        return Task.FromResult($"https://fakeblob.core.windows.net/{containerName}/{blobName}");
    }

    public Task<string?> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<bool> DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
