using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Workflows.Commands.DeleteWorkflow;

public sealed class DeleteWorkflowCommandHandler(
    IWorkflowRepository workflowRepository)
    : IRequestHandler<DeleteWorkflowCommand, Result>
{
    public async Task<Result> Handle(DeleteWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepository.GetByIdAsync(
            WorkflowId.From(request.WorkflowId), cancellationToken);

        if (workflow is null)
            return Error.NotFound("Workflow.NotFound", $"Workflow {request.WorkflowId} not found.");

        var result = workflow.Archive();
        if (result.IsFailure) return result;

        await workflowRepository.UpdateAsync(workflow, cancellationToken);
        return Result.Success();
    }
}
