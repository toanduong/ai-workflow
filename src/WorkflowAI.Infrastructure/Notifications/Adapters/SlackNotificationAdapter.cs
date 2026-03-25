using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Notifications.Adapters;

public sealed class SlackNotificationAdapter(
    HttpClient httpClient,
    ILogger<SlackNotificationAdapter> logger) : INotificationSender
{
    public async Task<bool> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                channel = request.RecipientAddress,
                text = request.Subject,
                blocks = BuildBlocks(request)
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://slack.com/api/chat.postMessage", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Slack notification to {Channel}", request.RecipientAddress);
            return false;
        }
    }

    private static object[] BuildBlocks(NotificationRequest request)
    {
        var blocks = new List<object>
        {
            new { type = "section", text = new { type = "mrkdwn", text = $"*{request.Subject}*\n{request.Body}" } }
        };

        if (request.ActionUrl is not null)
        {
            blocks.Add(new
            {
                type = "actions",
                elements = new object[]
                {
                    new { type = "button", text = new { type = "plain_text", text = "Approve" }, style = "primary", url = request.ActionUrl },
                    new { type = "button", text = new { type = "plain_text", text = "Reject" }, style = "danger", url = request.RejectUrl }
                }
            });
        }

        return blocks.ToArray();
    }
}
