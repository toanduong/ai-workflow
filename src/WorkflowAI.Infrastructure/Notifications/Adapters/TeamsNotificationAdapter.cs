using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Notifications.Adapters;

public sealed class TeamsNotificationAdapter(
    HttpClient httpClient,
    ILogger<TeamsNotificationAdapter> logger) : INotificationSender
{
    public async Task<bool> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var adaptiveCard = BuildAdaptiveCard(request);
            var json = JsonSerializer.Serialize(adaptiveCard);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(request.RecipientAddress, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Teams notification");
            return false;
        }
    }

    private static object BuildAdaptiveCard(NotificationRequest request)
    {
        var card = new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new { type = "TextBlock", text = request.Subject, weight = "Bolder", size = "Medium" },
                            new { type = "TextBlock", text = request.Body, wrap = true }
                        },
                        actions = request.ActionUrl is not null
                            ? new object[]
                            {
                                new { type = "Action.OpenUrl", title = "Approve", url = request.ActionUrl },
                                new { type = "Action.OpenUrl", title = "Reject", url = request.RejectUrl }
                            }
                            : Array.Empty<object>()
                    }
                }
            }
        };
        return card;
    }
}
