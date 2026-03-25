using MediatR;

namespace WorkflowAI.Domain.Common;

public interface IDomainEvent : INotification
{
    Guid EventId => Guid.NewGuid();
    DateTime OccurredAt => DateTime.UtcNow;
}
