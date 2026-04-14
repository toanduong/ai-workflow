using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Notifications.Adapters;

public sealed class EmailNotificationAdapter(
    EmailClient emailClient,
    ILogger<EmailNotificationAdapter> logger) : INotificationSender
{
    private const string SenderAddress = "DoNotReply@workflow-ai.com";

    public async Task<bool> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var emailMessage = new EmailMessage(
                senderAddress: SenderAddress,
                recipientAddress: request.RecipientAddress,
                content: new EmailContent(request.Subject) { Html = request.Body });

            await emailClient.SendAsync(WaitUntil.Started, emailMessage, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipient}", request.RecipientAddress);
            return false;
        }
    }
}
