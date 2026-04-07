using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.GenerateMcpWorkflows;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.TenantConnectors;

public class GenerateMcpWorkflowsCommandHandlerTests
{
    private readonly ITenantConnectorRepository _repository =
        Substitute.For<ITenantConnectorRepository>();
    private readonly IAnthropicService _anthropicService =
        Substitute.For<IAnthropicService>();

    private GenerateMcpWorkflowsCommandHandler CreateHandler() =>
        new(_repository, _anthropicService, NullLogger<GenerateMcpWorkflowsCommandHandler>.Instance);

    // Claude returns structured JSON with named arrays — the canonical response shape
    private static readonly string ValidClaudeResponse = """
        {
          "apiRoutes": [
            {
              "routeName": "connector/contacts-search",
              "method": "POST",
              "path": "/connectors/{connectorName}/contacts/search",
              "description": "Search contacts by name or email"
            },
            {
              "routeName": "connector/contacts-create",
              "method": "POST",
              "path": "/connectors/{connectorName}/contacts",
              "description": "Create a new contact"
            }
          ],
          "workflowTemplates": [
            {
              "templateName": "Sync New Contacts",
              "description": "Periodically pull new contacts and store them locally",
              "trigger": "schedule",
              "steps": [
                { "stepName": "FetchContacts", "routeName": "connector/contacts-search" }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenConnectorDoesNotExist()
    {
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        var result = await CreateHandler().Handle(
            new GenerateMcpWorkflowsCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnValidation_WhenConnectorIsNotActive()
    {
        var pendingConnector = new TenantConnectorBuilder().WithConnectorName("Stripe").Build();
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(pendingConnector);

        var result = await CreateHandler().Handle(
            new GenerateMcpWorkflowsCommand(pendingConnector.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotActive");
        await _anthropicService.DidNotReceive()
            .CompleteWithToolsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AIToolDefinition>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldGenerateRoutesAndTemplates_WhenConnectorIsActive()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Activated()
            .Build();
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);
        _repository.GetApisByConnectorAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TenantConnectorApi>());
        _anthropicService.CompleteWithToolsAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<AIToolDefinition>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(ValidClaudeResponse, 800, true));

        var result = await CreateHandler().Handle(
            new GenerateMcpWorkflowsCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ConnectorName.Should().Be("Apollo");
        result.Value.ApiRoutesCount.Should().Be(2);
        result.Value.WorkflowTemplatesCount.Should().Be(1);
        result.Value.ApiRoutes.Should().Contain("contacts-search");
        result.Value.WorkflowDefs.Should().Contain("Sync New Contacts");
    }

    [Fact]
    public async Task Handle_ShouldIncludeDiscoveredApisInPrompt_WhenApisExist()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("HubSpot")
            .Activated()
            .Build();
        var api = TenantConnectorApi.Create(
            connector.Id, connector.TenantId, "HubSpot",
            "contacts/search", "POST", "https://api.hubspot.com/crm/v3/objects/contacts/search", "{}");
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);
        _repository.GetApisByConnectorAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(new[] { api });
        _anthropicService.CompleteWithToolsAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<AIToolDefinition>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(ValidClaudeResponse, 800, true));

        await CreateHandler().Handle(
            new GenerateMcpWorkflowsCommand(connector.Id.Value), CancellationToken.None);

        // The prompt sent to Claude must contain the discovered API summary
        await _anthropicService.Received(1).CompleteWithToolsAsync(
            Arg.Is<string>(p => p.Contains("contacts/search") && p.Contains("HubSpot")),
            Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenClaudeFails()
    {
        var connector = new TenantConnectorBuilder().WithConnectorName("Salesforce").Activated().Build();
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);
        _repository.GetApisByConnectorAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TenantConnectorApi>());
        _anthropicService.CompleteWithToolsAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<AIToolDefinition>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 0, false, "rate limit exceeded"));

        var result = await CreateHandler().Handle(
            new GenerateMcpWorkflowsCommand(connector.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.GenerationFailed");
    }

    [Theory]
    [InlineData("Apollo")]
    [InlineData("HubSpot")]
    [InlineData("Salesforce")]
    [InlineData("Chatwoot")]
    [InlineData("Shopify")]
    public async Task Handle_ShouldWork_ForAnyActiveConnector_WithoutCodeChanges(string connectorName)
    {
        var connector = new TenantConnectorBuilder().WithConnectorName(connectorName).Activated().Build();
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(connector);
        _repository.GetApisByConnectorAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TenantConnectorApi>());
        _anthropicService.CompleteWithToolsAsync(
                Arg.Any<string>(), Arg.Any<IReadOnlyList<AIToolDefinition>>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(ValidClaudeResponse, 800, true));

        var result = await CreateHandler().Handle(
            new GenerateMcpWorkflowsCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue($"'{connectorName}' should generate workflows without code changes");
        result.Value!.ConnectorName.Should().Be(connectorName);
    }
}
