using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Domain.Workflows.Events;

namespace WorkflowAI.Application.Workflows.EventHandlers;

public sealed class WorkflowCreatedEventHandler(
    ILogger<WorkflowCreatedEventHandler> logger)
    : INotificationHandler<WorkflowCreatedEvent>
{
    public Task Handle(WorkflowCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Workflow created: {WorkflowId}", notification.WorkflowId);
        return Task.CompletedTask;
    }
}
