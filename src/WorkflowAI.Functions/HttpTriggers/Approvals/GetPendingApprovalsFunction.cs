using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Domain.Approvals;

namespace WorkflowAI.Functions.HttpTriggers.Approvals;

public sealed class GetPendingApprovalsFunction(IApprovalRepository approvalRepository)
{
    [Function("GetPendingApprovals")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "approvals/pending")]
        HttpRequestData req)
    {
        var pending = await approvalRepository.GetPendingAsync();
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(pending.Select(a => new
        {
            Id = a.Id.Value,
            a.Title,
            a.Description,
            Status = a.Status.Name,
            a.ExpiresAt
        }));
        return response;
    }
}
