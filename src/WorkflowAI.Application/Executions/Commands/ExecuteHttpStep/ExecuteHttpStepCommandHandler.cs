using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Application.Executions.Commands.AdvanceStep;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Executions.Commands.ExecuteHttpStep;

public sealed class ExecuteHttpStepCommandHandler(
    IExecutionRepository executionRepository,
    IWorkflowRepository workflowRepository,
    IConnectorCredentialResolver credentialResolver,
    HttpClient httpClient,
    IMediator mediator,
    ILogger<ExecuteHttpStepCommandHandler> logger)
    : IRequestHandler<ExecuteHttpStepCommand, Result>
{
    public async Task<Result> Handle(ExecuteHttpStepCommand request, CancellationToken ct)
    {
        var execution = await executionRepository.GetByIdAsync(ExecutionId.From(request.ExecutionId), ct);
        if (execution is null)
            return Error.NotFound("Execution.NotFound", "Execution not found.");

        var stepExecution = execution.Steps.FirstOrDefault(s => s.Id == request.StepExecutionId);
        if (stepExecution is null)
            return Error.NotFound("StepExecution.NotFound", "Step execution not found.");

        var workflow = await workflowRepository.GetByIdAsync(execution.WorkflowId, ct);
        if (workflow is null)
            return Error.NotFound("Workflow.NotFound", "Workflow not found.");

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepExecution.WorkflowStepId);
        if (step is null)
            return Error.NotFound("WorkflowStep.NotFound", "Workflow step not found.");

        stepExecution.Start();

        var url = step.Configuration ?? string.Empty;

        if (step.ConnectorId is not null)
        {
            var credentials = await credentialResolver.ResolveAsync(step.ConnectorId.Value, ct);
            if (credentials is not null)
                url = ApplyPlaceholders(url, credentials.Placeholders);
        }

        logger.LogInformation(
            "Executing HTTP step {StepName} against {Url}",
            step.Name, RedactUrl(url));

        string? responseBody = null;
        try
        {
            var response = await httpClient.GetAsync(url, ct);
            responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = $"HTTP {(int)response.StatusCode}: {responseBody}";
                stepExecution.Fail(error);
                await executionRepository.UpdateAsync(execution, ct);
                return Error.Unexpected("HttpStep.NonSuccess", error);
            }

            stepExecution.Complete(responseBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP step {StepName} failed", step.Name);
            stepExecution.Fail(ex.Message);
            await executionRepository.UpdateAsync(execution, ct);
            return Error.Unexpected("HttpStep.Exception", ex.Message);
        }

        await executionRepository.UpdateAsync(execution, ct);
        await mediator.Send(new AdvanceStepCommand(request.ExecutionId, step.Id), ct);

        return Result.Success();
    }

    private static string ApplyPlaceholders(string url, IReadOnlyDictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
            url = url.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        return url;
    }

    /// <summary>Redacts query string values to avoid leaking secrets in logs.</summary>
    private static string RedactUrl(string url)
    {
        var qIndex = url.IndexOf('?');
        return qIndex >= 0 ? url[..qIndex] + "?[redacted]" : url;
    }
}
