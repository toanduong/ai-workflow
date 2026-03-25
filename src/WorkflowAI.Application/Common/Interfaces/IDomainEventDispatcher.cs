using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Common.Interfaces;

public interface IDomainEventDispatcher
{
    Task DispatchEventsAsync<TId>(AggregateRoot<TId> aggregate, CancellationToken cancellationToken = default)
        where TId : notnull;
}
