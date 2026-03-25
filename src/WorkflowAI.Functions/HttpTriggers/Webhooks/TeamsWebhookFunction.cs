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

public sealed class TeamsWebhookFunction(
    IMediator mediator,
    IConfiguration configuration,
    ILogger<TeamsWebhookFunction> logger)
{
    [Function("TeamsWebhook")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/teams")]
        HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        req.Body.Position = 0;

        // Verify Teams HMAC signature
        var secret = configuration["Teams:WebhookSecret"];
        if (!string.IsNullOrEmpty(secret))
        {
            var authHeader = req.Headers.GetValues("Authorization").FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                logger.LogWarning("Missing Teams Authorization header");
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var computed = Convert.ToBase64String(hash);
            var provided = authHeader.Replace("HMAC ", "");

            if (!string.Equals(computed, provided, StringComparison.Ordinal))
            {
                logger.LogWarning("Invalid Teams HMAC signature");
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }
        }

        logger.LogInformation("Received verified Teams webhook");

        var payload = await req.ReadFromJsonAsync<TeamsActionPayload>();
        if (payload?.ApprovalRequestId is not null && payload.UserId is not null)
        {
            await mediator.Send(new ProcessApprovalCommand(
                payload.ApprovalRequestId.Value,
                payload.UserId.Value,
                payload.Action ?? "Approve",
                "Teams",
                payload.Comment));
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { Message = "Processed" });
        return response;
    }
}

internal sealed record TeamsActionPayload(
    Guid? ApprovalRequestId,
    Guid? UserId,
    string? Action,
    string? Comment);
