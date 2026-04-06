namespace WorkflowAI.Domain.Apollo;

public interface IApolloEventRepository
{
    Task AddAsync(ApolloEvent apolloEvent, CancellationToken cancellationToken = default);
    Task<ApolloEvent?> GetByIdAsync(ApolloEventId id, CancellationToken cancellationToken = default);
    Task UpdateAsync(ApolloEvent apolloEvent, CancellationToken cancellationToken = default);
    Task<List<ApolloEvent>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
