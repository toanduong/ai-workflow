using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Workflows.Commands.UpdateWorkflow;

public sealed record UpdateWorkflowCommand(Guid WorkflowId, string Name, string? Description) : IRequest<Result>;
