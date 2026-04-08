using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;
using WorkflowAI.Application.UnitTests.Common.Builders;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.UnitTests.TenantConnectors;

public class GenerateConnectorAssetsCommandHandlerTests
{
    private readonly ITenantConnectorRepository _repository = Substitute.For<ITenantConnectorRepository>();
    private readonly IAnthropicService _claudeAIService = Substitute.For<IAnthropicService>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    public GenerateConnectorAssetsCommandHandlerTests()
    {
        _currentUserService.IsAuthenticated.Returns(true);
    }

    // 3 api operations — no workflow templates (those belong to GenerateMcpWorkflows)
    private static readonly IReadOnlyList<AIToolCall> SampleToolCalls =
    [
        new AIToolCall("create_api_operation", """{"method":"GET","path":"/apollo/contacts/search","description":"Search Apollo contacts"}"""),
        new AIToolCall("create_api_operation", """{"method":"POST","path":"/apollo/sequences/enroll","description":"Enroll contact in sequence"}"""),
        new AIToolCall("create_api_operation", """{"method":"GET","path":"/apollo/people/match","description":"Find matching people"}""")
    ];

    private GenerateConnectorAssetsCommandHandler CreateHandler() =>
        new(_repository,
            _claudeAIService,
            _currentUserService,
            NullLogger<GenerateConnectorAssetsCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ActiveConnector_SavesApiOperationsToTenantConnectorApis()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Activated()
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);
        _repository.GetApisByConnectorAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns([]);

        _claudeAIService.CompleteWithToolsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 800, true, ToolCalls: SampleToolCalls));

        var result = await CreateHandler().Handle(
            new GenerateConnectorAssetsCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ApiRoutes.Should().NotBe("[]");
        result.Value.WorkflowDefs.Should().Be("[]",
            "workflow generation is GenerateMcpWorkflowsCommandHandler's responsibility");

        // 3 API operations → TenantConnectorApis only
        await _repository.Received(1).AddApisAsync(
            Arg.Is<IEnumerable<TenantConnectorApi>>(l => l.Count() == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActiveConnector_DoesNotSaveWorkflowTemplates()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Activated()
            .Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);
        _repository.GetApisByConnectorAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns([]);

        _claudeAIService.CompleteWithToolsAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 800, true, ToolCalls: SampleToolCalls));

        await CreateHandler().Handle(
            new GenerateConnectorAssetsCommand(connector.Id.Value), CancellationToken.None);

        // Only 1 tool — no create_workflow_template tool offered
        await _claudeAIService.Received(1).CompleteWithToolsAsync(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<AIToolDefinition>>(tools =>
                tools.Count == 1 && tools[0].Name == "create_api_operation"),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ApisAlreadyExist_SkipsClaudeAndReturnsExisting()
    {
        var connector = new TenantConnectorBuilder()
            .WithConnectorName("Apollo")
            .Activated()
            .Build();

        var existingApi = TenantConnectorApi.Create(
            connector.Id, connector.TenantId, "Apollo",
            "GET /contacts", "GET", "https://api.apollo.io/contacts", "{}");

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);
        _repository.GetApisByConnectorAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns([existingApi]);

        var result = await CreateHandler().Handle(
            new GenerateConnectorAssetsCommand(connector.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _claudeAIService.DidNotReceive().CompleteWithToolsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddApisAsync(
            Arg.Any<IEnumerable<TenantConnectorApi>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingConnector_ReturnsValidationError()
    {
        var connector = new TenantConnectorBuilder().WithConnectorName("Apollo").Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);

        var result = await CreateHandler().Handle(
            new GenerateConnectorAssetsCommand(connector.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotActive");
        await _claudeAIService.DidNotReceive().CompleteWithToolsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConnectorNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<TenantConnectorId>(), Arg.Any<CancellationToken>())
            .Returns((TenantConnector?)null);

        var result = await CreateHandler().Handle(
            new GenerateConnectorAssetsCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.NotFound");
    }

    [Fact]
    public async Task Handle_ClaudeFails_ReturnsFailureError()
    {
        var connector = new TenantConnectorBuilder().WithConnectorName("Apollo").Activated().Build();

        _repository.GetByIdAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns(connector);
        _repository.GetApisByConnectorAsync(connector.Id, Arg.Any<CancellationToken>())
            .Returns([]);

        _claudeAIService.CompleteWithToolsAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<AIToolDefinition>>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AICompletionResult(string.Empty, 0, false, "Model overloaded"));

        var result = await CreateHandler().Handle(
            new GenerateConnectorAssetsCommand(connector.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TenantConnector.AssetGenerationFailed");
        await _repository.DidNotReceive().AddApisAsync(
            Arg.Any<IEnumerable<TenantConnectorApi>>(), Arg.Any<CancellationToken>());
    }
}
