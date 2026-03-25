using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace WorkflowAI.Functions.HttpTriggers.Health;

public sealed class HealthCheckFunction
{
    [Function("HealthCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
        HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "WorkflowAI.Functions"
        });
        return response;
    }
}
