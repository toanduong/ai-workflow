using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.Channels;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlChannelRepository(WorkflowAIDbContext context) : IChannelRepository
{
    public async Task<NotificationChannel?> GetByIdAsync(ChannelId id, CancellationToken cancellationToken = default)
        => await context.Channels.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<NotificationChannel>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await context.Channels.Where(c => c.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificationChannel>> GetByTypeAsync(ChannelType type, CancellationToken cancellationToken = default)
        => await context.Channels.Where(c => c.ChannelType == type && c.IsActive).ToListAsync(cancellationToken);

    public async Task AddAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        context.Channels.Add(channel);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        context.Channels.Update(channel);
        await context.SaveChangesAsync(cancellationToken);
    }
}
