using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Infrastructure.Notifications.Adapters;

namespace WorkflowAI.Infrastructure.Notifications;

public sealed class NotificationRouter(
    IServiceProvider serviceProvider,
    ILogger<NotificationRouter> logger) : INotificationSender
{
    public async Task<bool> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var adapter = ResolveAdapter(request.ChannelType);
        if (adapter is null)
        {
            logger.LogWarning("No adapter found for channel type: {ChannelType}", request.ChannelType);
            return false;
        }

        return await adapter.SendAsync(request, cancellationToken);
    }

    private INotificationSender? ResolveAdapter(string channelType)
    {
        return channelType.ToLowerInvariant() switch
        {
            "email" => serviceProvider.GetService<EmailNotificationAdapter>(),
            "slack" => serviceProvider.GetService<SlackNotificationAdapter>(),
            "teams" => serviceProvider.GetService<TeamsNotificationAdapter>(),
            _ => null
        };
    }
}
