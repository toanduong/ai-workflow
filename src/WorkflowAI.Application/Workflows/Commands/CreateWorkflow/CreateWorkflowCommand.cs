using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Workflows.Commands.CreateWorkflow;

public sealed record CreateWorkflowCommand(
    string Name,
    string? Description,
    Guid? TemplateId,
    List<CreateWorkflowStepDto>? Steps) : IRequest<Result<WorkflowId>>;

public sealed record CreateWorkflowStepDto(
    string Name,
    string StepType,
    string? Configuration,
    string? RequiredRole,
    int TimeoutMinutes = 60,
    string? OnTimeoutAction = null);
