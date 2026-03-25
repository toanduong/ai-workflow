namespace WorkflowAI.Domain.Channels;

public interface IChannelRepository
{
    Task<NotificationChannel?> GetByIdAsync(ChannelId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationChannel>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationChannel>> GetByTypeAsync(ChannelType type, CancellationToken cancellationToken = default);
    Task AddAsync(NotificationChannel channel, CancellationToken cancellationToken = default);
    Task UpdateAsync(NotificationChannel channel, CancellationToken cancellationToken = default);
}
