using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.Apollo;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlApolloEventRepository(WorkflowAIDbContext context) : IApolloEventRepository
{
    public async Task AddAsync(ApolloEvent apolloEvent, CancellationToken cancellationToken = default)
    {
        context.ApolloEvents.Add(apolloEvent);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApolloEvent?> GetByIdAsync(ApolloEventId id, CancellationToken cancellationToken = default)
        => await context.ApolloEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task UpdateAsync(ApolloEvent apolloEvent, CancellationToken cancellationToken = default)
    {
        context.ApolloEvents.Update(apolloEvent);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ApolloEvent>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        => await context.ApolloEvents
            .OrderByDescending(e => e.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
}
