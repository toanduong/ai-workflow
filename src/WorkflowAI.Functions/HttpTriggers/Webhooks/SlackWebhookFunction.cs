using System.Net;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Approvals.Commands.ProcessApproval;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Webhooks;

public sealed class SlackWebhookFunction(
    IMediator mediator,
    IConfiguration configuration,
    ILogger<SlackWebhookFunction> logger)
{
    [Function("SlackWebhook")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/slack")]
        HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        req.Body.Position = 0;

        // Verify Slack signing secret
        var signingSecret = configuration["Slack:SigningSecret"];
        if (!string.IsNullOrEmpty(signingSecret))
        {
            var timestamp = req.Headers.GetValues("X-Slack-Request-Timestamp").FirstOrDefault();
            var signature = req.Headers.GetValues("X-Slack-Signature").FirstOrDefault();

            if (timestamp is null || signature is null)
            {
                logger.LogWarning("Missing Slack signature headers");
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            var baseString = $"v0:{timestamp}:{body}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
            var computed = $"v0={Convert.ToHexStringLower(hash)}";

            if (!string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Invalid Slack signature");
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }
        }

        logger.LogInformation("Received verified Slack webhook");

        // Parse interaction payload and dispatch approval
        var payload = await req.ReadFromJsonAsync<SlackInteractionPayload>();
        if (payload?.ApprovalRequestId is not null && payload.UserId is not null)
        {
            await mediator.Send(new ProcessApprovalCommand(
                payload.ApprovalRequestId.Value,
                payload.UserId.Value,
                payload.Action ?? "Approve",
                "Slack",
                payload.Comment));
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { Message = "Processed" });
        return response;
    }
}

internal sealed record SlackInteractionPayload(
    Guid? ApprovalRequestId,
    Guid? UserId,
    string? Action,
    string? Comment);
