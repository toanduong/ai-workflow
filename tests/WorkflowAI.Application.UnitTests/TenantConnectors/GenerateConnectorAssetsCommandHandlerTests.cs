using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.Templates;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.TenantConnectors;

public class GenerateConnectorAssetsCommandHandlerTests
{
    private readonly ITenantConnectorRepository _repository = Substitute.For<ITenantConnectorRepository>();
    private readonly ITemplateRepository _templateRepository = Substitute.For<ITemplateRepository>();
    private readonly IClaudeAIService _claudeAIService = Substitute.For<IClaudeAIService>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    public GenerateConnectorAssetsCommandHandlerTests()
    {
        _currentUserService.IsAuthenticated.Returns(true);
    }

    private static readonly IReadOnlyList<AIToolCall> SampleToolCalls = new[]
    {
        new AIToolCall("create_api_route", """{"method":"GET","path":"/apollo/contacts/search","description":"Search Apollo contacts"}"""),
        new AIToolCall("create_api_route", """{"method":"POST","path":"/apollo/sequences/enroll","description":"Enroll contact in sequence"}"""),
        new AIToolCall("create_api_route", """{"method":"GET","path":"/apollo/people/match","description":"Find matching people"}"""),
        new AIToolCall("create_workflow_template", """{"name":"Apollo Lead Enrich","trigger":"webhook","steps":[],"description":"Enrich leads from Apollo"}"""),
        new AIToolCall("create_workflow_template", """{"name":"Apollo Sequence Trigger","trigger":"schedule","steps":[],"description":"Trigger Apollo sequences"}""")
    };

    private GenerateConnectorAssetsCommandHandler CreateHandler() =>
        new(_repository,
            _templateRepository,
            _claudeAIService,
            _currentUserService,
            NullLogger<GenerateConnectorAssetsCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ActiveApolloConnector_ReturnsApiRoutesAndWorkflows()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Activated()
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        _claudeAIService.CompleteWithToolsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 800, true, ToolCalls: SampleToolCalls));

        var command = new GenerateConnectorAssetsCommand(connector.Id.Value);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiRoutes.Should().NotBeEmpty();
        result.Value.WorkflowDefs.Should().NotBeEmpty();

        // 3 api routes + 2 workflow templates = 5 templates saved
        await _templateRepository.Received(5).AddAsync(
            Arg.Any<WorkflowTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActiveConnector_SavesApiRoutesWithCorrectCategory()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Activated()
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        _claudeAIService.CompleteWithToolsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 800, true, ToolCalls: SampleToolCalls));

        var command = new GenerateConnectorAssetsCommand(connector.Id.Value);
        await CreateHandler().Handle(command, CancellationToken.None);

        await _templateRepository.Received(3).AddAsync(
            Arg.Is<WorkflowTemplate>(t => t.Category == "Apollo/ApiRoute"),
            Arg.Any<CancellationToken>());

        await _templateRepository.Received(2).AddAsync(
            Arg.Is<WorkflowTemplate>(t => t.Category == "Apollo/Workflow"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingConnector_ReturnsValidationError()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Build(); // Pending by default

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        var command = new GenerateConnectorAssetsCommand(connector.Id.Value);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotActive");
        await _claudeAIService.DidNotReceive().CompleteWithToolsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<CancellationToken>());
        await _templateRepository.DidNotReceive().AddAsync(
            Arg.Any<WorkflowTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedConnector_ReturnsValidationError()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Failed("Bad API key")
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        var command = new GenerateConnectorAssetsCommand(connector.Id.Value);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotActive");
    }

    [Fact]
    public async Task Handle_ConnectorNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        var command = new GenerateConnectorAssetsCommand(Guid.NewGuid());
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotFound");
    }

    [Fact]
    public async Task Handle_ClaudeFails_ReturnsFailureError()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Activated()
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        _claudeAIService.CompleteWithToolsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 0, false, "Model overloaded"));

        var command = new GenerateConnectorAssetsCommand(connector.Id.Value);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.AssetGenerationFailed");
        await _templateRepository.DidNotReceive().AddAsync(
            Arg.Any<WorkflowTemplate>(), Arg.Any<CancellationToken>());
    }
}
