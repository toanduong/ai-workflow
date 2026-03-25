using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Workflows.Commands.CreateWorkflow;

public sealed class CreateWorkflowCommandHandler(
    IWorkflowRepository workflowRepository,
    IDomainEventDispatcher eventDispatcher,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateWorkflowCommand, Result<WorkflowId>>
{
    public async Task<Result<WorkflowId>> Handle(CreateWorkflowCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
            return Error.Unauthorized("Auth.Required", "User must be authenticated.");

        var workflow = Workflow.Create(
            request.Name,
            request.Description,
            currentUserService.UserId.Value,
            request.TemplateId);

        if (request.Steps is not null)
        {
            foreach (var stepDto in request.Steps)
            {
                var stepType = StepType.FromName(stepDto.StepType);
                if (stepType is null)
                    return Error.Validation("Step.InvalidType", $"Invalid step type: {stepDto.StepType}");

                var timeoutAction = stepDto.OnTimeoutAction is not null
                    ? TimeoutAction.FromName(stepDto.OnTimeoutAction)
                    : null;

                var result = workflow.AddStep(
                    stepDto.Name,
                    stepType,
                    stepDto.Configuration,
                    stepDto.RequiredRole,
                    stepDto.TimeoutMinutes,
                    timeoutAction);

                if (result.IsFailure)
                    return Result<WorkflowId>.Failure(result.Error!);
            }
        }

        await workflowRepository.AddAsync(workflow, cancellationToken);
        await eventDispatcher.DispatchEventsAsync(workflow, cancellationToken);

        return workflow.Id;
    }
}
