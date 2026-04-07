using System.Net;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Executions.Commands.AdvanceStep;
using WorkflowAI.Application.Executions.Commands.ExecuteHttpStep;
using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Users;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.UnitTests.Executions;

public class ExecuteHttpStepCommandHandlerTests
{
    private readonly IExecutionRepository _executionRepository = Substitute.For<IExecutionRepository>();
    private readonly IWorkflowRepository _workflowRepository = Substitute.For<IWorkflowRepository>();
    private readonly IConnectorCredentialResolver _credentialResolver = Substitute.For<IConnectorCredentialResolver>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private (ExecuteHttpStepCommandHandler Handler, FakeHttpHandler HttpHandler) CreateHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string responseBody = "{\"data\":[]}")
    {
        var fakeHttp = new FakeHttpHandler(statusCode, responseBody);
        var httpClient = new HttpClient(fakeHttp);
        var handler = new ExecuteHttpStepCommandHandler(
            _executionRepository,
            _workflowRepository,
            _credentialResolver,
            httpClient,
            _mediator,
            NullLogger<ExecuteHttpStepCommandHandler>.Instance);
        return (handler, fakeHttp);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: build a workflow step + execution pair ready for testing
    // ─────────────────────────────────────────────────────────────────────────
    private static (Workflow Workflow, WorkflowExecution Execution, StepExecution StepExecution) BuildScenario(
        string stepUrl,
        ConnectorId? connectorId = null)
    {
        var workflow = Workflow.Create("Test", null, UserId.New());
        workflow.AddStep("Step1", StepType.Notification, configuration: stepUrl);
        var step = workflow.Steps[0];
        if (connectorId.HasValue) step.SetConnector(connectorId.Value);

        var execution = WorkflowExecution.Create(workflow.Id, "Test");
        var stepExecution = execution.AddStep(step.Id);

        return (workflow, execution, stepExecution);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: placeholders replaced, HTTP call fires, step marked complete
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_ShouldReplacePlaceholdersAndCompleteStep_WhenConnectorCredentialsResolved()
    {
        var connectorId = ConnectorId.New();
        var (handler, fakeHttp) = CreateHandler();

        var (workflow, execution, stepExecution) = BuildScenario(
            "https://pages.fm/api/v2/pages/{page_id}/conversations?page_access_token={page_access_token}",
            connectorId);

        _executionRepository.GetByIdAsync(Arg.Any<ExecutionId>(), Arg.Any<CancellationToken>())
            .Returns(execution);
        _workflowRepository.GetByIdAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns(workflow);
        _credentialResolver.ResolveAsync(connectorId, Arg.Any<CancellationToken>())
            .Returns(new ConnectorCredentials(new Dictionary<string, string>
            {
                ["page_id"] = "page123",
                ["page_access_token"] = "secret-abc"
            }));
        _mediator.Send(Arg.Any<AdvanceStepCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await handler.Handle(
            new ExecuteHttpStepCommand(execution.Id.Value, stepExecution.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // Verify URL had placeholders replaced
        var requestedUrl = fakeHttp.LastRequestedUrl!.AbsoluteUri;
        requestedUrl.Should().Contain("page123");
        requestedUrl.Should().Contain("secret-abc");
        requestedUrl.Should().NotContain("{page_id}");
        requestedUrl.Should().NotContain("{page_access_token}");

        // Step should be marked complete
        stepExecution.Status.Should().Be(StepExecutionStatus.Completed);
        stepExecution.OutputData.Should().NotBeNull();

        // Advance next step should be dispatched
        await _mediator.Received(1).Send(
            Arg.Is<AdvanceStepCommand>(c => c.ExecutionId == execution.Id.Value),
            Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: step has no connector → URL used as-is
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_ShouldExecuteRawUrl_WhenNoConnectorId()
    {
        var (handler, fakeHttp) = CreateHandler();

        var (workflow, execution, stepExecution) = BuildScenario("https://api.example.com/items");

        _executionRepository.GetByIdAsync(Arg.Any<ExecutionId>(), Arg.Any<CancellationToken>())
            .Returns(execution);
        _workflowRepository.GetByIdAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns(workflow);
        _mediator.Send(Arg.Any<AdvanceStepCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await handler.Handle(
            new ExecuteHttpStepCommand(execution.Id.Value, stepExecution.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fakeHttp.LastRequestedUrl!.AbsoluteUri.Should().Be("https://api.example.com/items");
        await _credentialResolver.DidNotReceive()
            .ResolveAsync(Arg.Any<ConnectorId>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: HTTP 4xx → step fails, error returned
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_ShouldFailStep_WhenHttpReturnsNonSuccess()
    {
        var (handler, _) = CreateHandler(HttpStatusCode.Unauthorized, "Unauthorized");

        var (workflow, execution, stepExecution) = BuildScenario("https://api.example.com/items");

        _executionRepository.GetByIdAsync(Arg.Any<ExecutionId>(), Arg.Any<CancellationToken>())
            .Returns(execution);
        _workflowRepository.GetByIdAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns(workflow);

        var result = await handler.Handle(
            new ExecuteHttpStepCommand(execution.Id.Value, stepExecution.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("HttpStep.NonSuccess");

        stepExecution.Status.Should().Be(StepExecutionStatus.Failed);

        // AdvanceStep should NOT be called on failure
        await _mediator.DidNotReceive().Send(
            Arg.Any<AdvanceStepCommand>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: execution not found → returns not-found error
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenExecutionDoesNotExist()
    {
        var (handler, _) = CreateHandler();

        _executionRepository.GetByIdAsync(Arg.Any<ExecutionId>(), Arg.Any<CancellationToken>())
            .Returns((WorkflowExecution?)null);

        var result = await handler.Handle(
            new ExecuteHttpStepCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Execution.NotFound");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5: step execution id does not match → returns not-found error
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenStepExecutionDoesNotExist()
    {
        var (handler, _) = CreateHandler();
        var (_, execution, _) = BuildScenario("https://api.example.com");

        _executionRepository.GetByIdAsync(Arg.Any<ExecutionId>(), Arg.Any<CancellationToken>())
            .Returns(execution);

        var result = await handler.Handle(
            new ExecuteHttpStepCommand(execution.Id.Value, Guid.NewGuid()),  // wrong StepExecutionId
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("StepExecution.NotFound");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6: HttpClient throws → step fails, exception handled gracefully
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_ShouldFailStep_WhenHttpClientThrows()
    {
        var fakeHttp = new ThrowingHttpHandler();
        var httpClient = new HttpClient(fakeHttp);
        var handler = new ExecuteHttpStepCommandHandler(
            _executionRepository, _workflowRepository, _credentialResolver, httpClient,
            _mediator, NullLogger<ExecuteHttpStepCommandHandler>.Instance);

        var (workflow, execution, stepExecution) = BuildScenario("https://api.example.com");

        _executionRepository.GetByIdAsync(Arg.Any<ExecutionId>(), Arg.Any<CancellationToken>())
            .Returns(execution);
        _workflowRepository.GetByIdAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns(workflow);

        var result = await handler.Handle(
            new ExecuteHttpStepCommand(execution.Id.Value, stepExecution.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("HttpStep.Exception");
        stepExecution.Status.Should().Be(StepExecutionStatus.Failed);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fake HTTP handlers
    // ─────────────────────────────────────────────────────────────────────────
    internal sealed class FakeHttpHandler(HttpStatusCode statusCode, string responseBody)
        : HttpMessageHandler
    {
        public Uri? LastRequestedUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestedUrl = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            });
        }
    }

    internal sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("Connection refused");
    }
}
