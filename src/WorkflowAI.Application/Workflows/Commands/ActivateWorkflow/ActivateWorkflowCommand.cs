using MediatR;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Workflows.Commands.ActivateWorkflow;

public sealed record ActivateWorkflowCommand(Guid WorkflowId) : IRequest<Result<ActivateWorkflowResult>>;

public sealed record ActivateWorkflowResult(string ArmTemplateContent, string FileName, string? LogicAppResourceId);
