using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Infrastructure.Messaging;

public sealed class DomainEventDispatcher(
    IMediator mediator,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchEventsAsync<TId>(AggregateRoot<TId> aggregate, CancellationToken cancellationToken = default)
        where TId : notnull
    {
        var domainEvents = aggregate.DomainEvents.ToList();
        aggregate.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
        {
            logger.LogInformation("Dispatching domain event: {EventType}", domainEvent.GetType().Name);
            await mediator.Publish(domainEvent, cancellationToken);
        }
    }
}
