using WorkflowAI.Domain.Notifications;

namespace WorkflowAI.Infrastructure.Persistence.BlobStorage.Repositories;

internal sealed class BlobNotificationRepository(BlobStorageContext context)
    : BlobRepositoryBase(context.Notifications), INotificationRepository
{
    public Task<Notification?> GetByIdAsync(NotificationId id, CancellationToken ct = default)
        => GetAsync<Notification>(id.Value, ct);

    public async Task<IReadOnlyList<Notification>> GetFailedForRetryAsync(int maxRetryCount, CancellationToken ct = default)
    {
        var all = await GetAllAsync<Notification>(ct);
        return all.Where(n => n.Status == NotificationStatus.Failed && n.RetryCount < maxRetryCount)
            .ToList().AsReadOnly();
    }

    public Task AddAsync(Notification notification, CancellationToken ct = default)
        => SaveAsync(notification.Id.Value, notification, overwrite: false, ct);

    public Task UpdateAsync(Notification notification, CancellationToken ct = default)
        => SaveAsync(notification.Id.Value, notification, overwrite: true, ct);
}
