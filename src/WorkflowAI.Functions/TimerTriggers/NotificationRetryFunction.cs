using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Notifications;

namespace WorkflowAI.Functions.TimerTriggers;

public sealed class NotificationRetryFunction(
    INotificationRepository notificationRepository,
    INotificationSender notificationSender,
    ILogger<NotificationRetryFunction> logger)
{
    private const int MaxRetryCount = 4;

    [Function("RetryFailedNotifications")]
    public async Task Run(
        [TimerTrigger("0 */10 * * * *")] TimerInfo timer,
        FunctionContext context)
    {
        logger.LogInformation("Retrying failed notifications...");

        var failedNotifications = await notificationRepository.GetFailedForRetryAsync(MaxRetryCount);

        foreach (var notification in failedNotifications)
        {
            logger.LogInformation(
                "Retrying notification {NotificationId}, attempt {RetryCount}",
                notification.Id.Value,
                notification.RetryCount + 1);

            var request = new NotificationRequest(
                ChannelType: "email", // TODO: Resolve from notification channel config
                RecipientAddress: notification.RecipientAddress,
                Subject: notification.Subject,
                Body: notification.Body);

            var success = await notificationSender.SendAsync(request);
            if (success)
                notification.MarkSent();
            else
                notification.MarkFailed();

            await notificationRepository.UpdateAsync(notification);
        }

        logger.LogInformation("Processed {Count} failed notifications.", failedNotifications.Count);
    }
}
