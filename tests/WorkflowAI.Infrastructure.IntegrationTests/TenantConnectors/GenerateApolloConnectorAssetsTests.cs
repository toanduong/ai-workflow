using Anthropic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;
using WorkflowAI.Domain.Templates;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.AI;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Integration tests that call the real Claude API to generate API routes and
/// workflow templates for an Apollo connector.
///
/// Requirements:
///   ANTHROPIC_API_KEY environment variable must be set.
///
/// These tests do NOT hit the Apollo API or a database — they verify that
/// Claude generates meaningful, well-structured output given Apollo metadata.
/// Each test calls Claude independently so they can be run in any order.
/// </summary>
[Trait("Category", "Integration")]
public class GenerateApolloConnectorAssetsTests
{
    private static readonly string AnthropicApiKey =
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        ?? throw new InvalidOperationException(
            "ANTHROPIC_API_KEY environment variable is not set.");

    // Pre-baked Apollo metadata matching what ProvisionTenantConnector would store
    private const string ApolloMetadata = """
        {
          "authType": "APIKey",
          "requiredFields": ["api_key"],
          "testEndpoint": {"method": "GET", "path": "https://api.apollo.io/v1/auth/health"},
          "endpoints": {
            "searchPeople":   {"method": "POST", "path": "/v1/mixed_people/search"},
            "matchPerson":    {"method": "POST", "path": "/v1/people/match"},
            "searchOrgs":     {"method": "POST", "path": "/v1/mixed_companies/search"},
            "enrollSequence": {"method": "POST", "path": "/v1/emailer_campaigns/{id}/add_contact_ids"},
            "getContact":     {"method": "GET",  "path": "/v1/contacts/{id}"}
          },
          "configSchema": {
            "type": "object",
            "properties": {"api_key": {"type": "string", "description": "Apollo API key"}}
          }
        }
        """;

    private const string ApolloInfo = """
        {
          "description": "Apollo.io is a sales intelligence and engagement platform with a database of 275M+ contacts.",
          "docsUrl": "https://apolloio.github.io/apollo-api-docs/",
          "capabilities": ["contact_search", "company_search", "email_sequences", "crm_sync", "lead_enrichment"],
          "rateLimits": "50 requests/minute on free tier, 200 on paid",
          "webhookSupport": true
        }
        """;

    /// <summary>
    /// Creates a fresh handler + capturing template list for each test.
    /// Returns both so the test can inspect what was saved.
    /// </summary>
    private (GenerateConnectorAssetsCommandHandler handler, List<WorkflowTemplate> captured, Guid connectorId)
        CreateHandlerWithCapture()
    {
        var connector = TenantConnector.Create(TenantId.New(), "Apollo");
        connector.SetMetadata(ApolloMetadata, ApolloInfo);
        connector.Activate();

        var repo = Substitute.For<ITenantConnectorRepository>();
        repo.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>()).Returns(connector);

        var captured = new List<WorkflowTemplate>();
        var templateRepo = Substitute.For<ITemplateRepository>();
        templateRepo
            .AddAsync(Arg.Do<WorkflowTemplate>(t => captured.Add(t)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var claudeClient = new AnthropicClient(
            new Anthropic.Core.ClientOptions { ApiKey = AnthropicApiKey });
        var claudeOptions = Options.Create(new ClaudeAIOptions
        {
            Model = "claude-opus-4-6",
            MaxTokens = 16000
        });
        var claudeService = new ClaudeAIService(
            claudeClient, claudeOptions, NullLogger<ClaudeAIService>.Instance);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.IsAuthenticated.Returns(true);

        var handler = new GenerateConnectorAssetsCommandHandler(
            repo, templateRepo, claudeService, currentUser,
            NullLogger<GenerateConnectorAssetsCommandHandler>.Instance);

        return (handler, captured, connector.Id.Value);
    }

    // ── Test 1: overall success ───────────────────────────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_ForApollo_ReturnsSuccessResult()
    {
        var (handler, _, connectorId) = CreateHandlerWithCapture();

        var result = await handler.Handle(
            new GenerateConnectorAssetsCommand(connectorId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "Claude should successfully generate routes and workflows for Apollo");
        result.Value!.ApiRoutes.Should().NotBe("[]",
            "at least one API route should be generated");
        result.Value.WorkflowDefs.Should().NotBe("[]",
            "at least one workflow template should be generated");
    }

    // ── Test 2: minimum 3 API routes ─────────────────────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_ForApollo_SavesAtLeastThreeApiRoutes()
    {
        var (handler, captured, connectorId) = CreateHandlerWithCapture();

        await handler.Handle(
            new GenerateConnectorAssetsCommand(connectorId), CancellationToken.None);

        captured.Where(t => t.Category == "Apollo/ApiRoute")
            .Should().HaveCountGreaterThanOrEqualTo(3,
                "the prompt instructs Claude to generate at least 3 API routes");
    }

    // ── Test 3: minimum 2 workflow templates ─────────────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_ForApollo_SavesAtLeastTwoWorkflowTemplates()
    {
        var (handler, captured, connectorId) = CreateHandlerWithCapture();

        await handler.Handle(
            new GenerateConnectorAssetsCommand(connectorId), CancellationToken.None);

        captured.Where(t => t.Category == "Apollo/Workflow")
            .Should().HaveCountGreaterThanOrEqualTo(2,
                "the prompt instructs Claude to generate at least 2 workflow templates");
    }

    // ── Test 4: API route structure ───────────────────────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_ForApollo_ApiRoutesHaveValidStructure()
    {
        var (handler, captured, connectorId) = CreateHandlerWithCapture();

        await handler.Handle(
            new GenerateConnectorAssetsCommand(connectorId), CancellationToken.None);

        var apiRoutes = captured.Where(t => t.Category == "Apollo/ApiRoute").ToList();
        apiRoutes.Should().NotBeEmpty();

        foreach (var route in apiRoutes)
        {
            route.Name.Should().StartWith("Apollo:",
                "each API route name should be prefixed with the connector name");
            route.Name.Should().MatchRegex(@"Apollo: (GET|POST|PUT|PATCH|DELETE) ",
                "route name should include HTTP method");
            route.DefaultSteps.Should().NotBeNullOrEmpty();

            var steps = JsonDocument.Parse(route.DefaultSteps!).RootElement;
            steps.ValueKind.Should().Be(JsonValueKind.Array,
                "DefaultSteps should be a JSON array");
            steps.GetArrayLength().Should().Be(1,
                "each API route generates one action step");
        }
    }

    // ── Test 5: workflow template structure ───────────────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_ForApollo_WorkflowTemplatesHaveValidStructure()
    {
        var (handler, captured, connectorId) = CreateHandlerWithCapture();

        await handler.Handle(
            new GenerateConnectorAssetsCommand(connectorId), CancellationToken.None);

        var workflows = captured.Where(t => t.Category == "Apollo/Workflow").ToList();
        workflows.Should().NotBeEmpty();

        foreach (var wf in workflows)
        {
            wf.Name.Should().StartWith("Apollo:",
                "each workflow name should be prefixed with the connector name");
            wf.DefaultSteps.Should().NotBeNullOrEmpty();

            var steps = JsonDocument.Parse(wf.DefaultSteps!).RootElement;
            steps.ValueKind.Should().Be(JsonValueKind.Array,
                "workflow DefaultSteps should be a JSON array");
        }
    }

    // ── Test 6: Apollo-relevant content ──────────────────────────────────────

    [Fact]
    public async Task GenerateConnectorAssets_ForApollo_GeneratesApolloRelevantRoutes()
    {
        var (handler, captured, connectorId) = CreateHandlerWithCapture();

        await handler.Handle(
            new GenerateConnectorAssetsCommand(connectorId), CancellationToken.None);

        var allNames = captured.Select(t => t.Name).ToList();

        allNames.Should().Contain(n =>
            n.Contains("people", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("contact", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("search", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("sequence", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("enrich", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("organization", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("company", StringComparison.OrdinalIgnoreCase),
            $"generated names should reflect Apollo capabilities. Got: {string.Join(", ", allNames)}");
    }
}
