using Microsoft.Azure.Cosmos;
using WorkflowAI.Domain.Notifications;

namespace WorkflowAI.Infrastructure.Persistence.Cosmos.Repositories;

public sealed class CosmosNotificationRepository(CosmosDbContext context) : INotificationRepository
{
    public async Task<Notification?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.Value.ToString());

        var iterator = context.Notifications.GetItemQueryIterator<Notification>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var result = response.FirstOrDefault();
            if (result is not null) return result;
        }
        return null;
    }

    public async Task<IReadOnlyList<Notification>> GetFailedForRetryAsync(int maxRetryCount, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.status.name = 'Failed' AND c.retryCount < @maxRetry")
            .WithParameter("@maxRetry", maxRetryCount);

        var iterator = context.Notifications.GetItemQueryIterator<Notification>(query);
        var results = new List<Notification>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }
        return results.AsReadOnly();
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await context.Notifications.CreateItemAsync(
            notification,
            new PartitionKey(notification.ApprovalRequestId?.ToString() ?? "none"),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await context.Notifications.UpsertItemAsync(
            notification,
            new PartitionKey(notification.ApprovalRequestId?.ToString() ?? "none"),
            cancellationToken: cancellationToken);
    }
}
