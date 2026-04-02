using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.Notifications;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlNotificationRepository(WorkflowAIDbContext context) : INotificationRepository
{
    public async Task<Notification?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken = default)
        => await context.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetFailedForRetryAsync(int maxRetryCount, CancellationToken cancellationToken = default)
        => await context.Notifications
            .Where(n => n.Status == NotificationStatus.Failed && n.RetryCount < maxRetryCount)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        context.Notifications.Update(notification);
        await context.SaveChangesAsync(cancellationToken);
    }
}
