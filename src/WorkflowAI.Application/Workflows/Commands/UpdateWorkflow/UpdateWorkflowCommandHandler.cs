using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Workflows.Commands.UpdateWorkflow;

public sealed class UpdateWorkflowCommandHandler(
    IWorkflowRepository workflowRepository)
    : IRequestHandler<UpdateWorkflowCommand, Result>
{
    public async Task<Result> Handle(UpdateWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepository.GetByIdAsync(
            WorkflowId.From(request.WorkflowId), cancellationToken);

        if (workflow is null)
            return Error.NotFound("Workflow.NotFound", $"Workflow {request.WorkflowId} not found.");

        workflow.Update(request.Name, request.Description);
        await workflowRepository.UpdateAsync(workflow, cancellationToken);

        return Result.Success();
    }
}
