using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Approvals;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Functions.HttpTriggers.Approvals;

public sealed class TokenApprovalFunction(
    ITokenService tokenService,
    IApprovalRepository approvalRepository,
    IDomainEventDispatcher eventDispatcher)
{
    [Function("TokenApproval")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "approvals/act")]
        HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var token = query["token"];

        if (string.IsNullOrEmpty(token))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { Message = "Token is required." });
            return badRequest;
        }

        var payload = tokenService.ValidateApprovalToken(token);
        if (payload is null)
        {
            var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteAsJsonAsync(new { Message = "Invalid or expired token." });
            return unauthorized;
        }

        var approval = await approvalRepository.GetByIdAsync(
            ApprovalRequestId.From(payload.ApprovalRequestId));

        if (approval is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { Message = "Approval request not found." });
            return notFound;
        }

        var result = payload.Action.ToLowerInvariant() == "approve"
            ? approval.Approve(UserId.From(Guid.Empty), ApprovalChannel.Email)
            : approval.Reject(UserId.From(Guid.Empty), ApprovalChannel.Email);

        if (result.IsFailure)
        {
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteAsJsonAsync(new { result.Error!.Code, result.Error.Message });
            return conflict;
        }

        await approvalRepository.UpdateAsync(approval);
        await eventDispatcher.DispatchEventsAsync(approval);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { Message = $"Approval {payload.Action}d successfully." });
        return response;
    }
}
