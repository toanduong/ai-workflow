using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WorkflowAI.Application.Workflows.Commands.GenerateArmTemplate;
using WorkflowAI.Functions.Extensions;

namespace WorkflowAI.Functions.HttpTriggers.Workflows;

public sealed class GenerateArmTemplateFromPromptFunction(IMediator mediator)
{
    [Function("GenerateArmTemplateFromPrompt")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId}/workflows/generate")]
        HttpRequestData req,
        Guid tenantId)
    {
        var body = await req.ReadFromJsonAsync<GenerateArmTemplateFromPromptRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Prompt))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { Message = "Request body must contain a non-empty 'prompt'." });
            return badRequest;
        }

        // Validate deployment config if deploy is requested
        if (body.Deploy && body.DeploymentConfig is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { Message = "DeploymentConfig is required when Deploy is true." });
            return badRequest;
        }

        // Map DTO to command parameter
        DeploymentConfig? deploymentConfig = null;
        if (body.DeploymentConfig is not null)
        {
            deploymentConfig = new DeploymentConfig(
                body.DeploymentConfig.SubscriptionId,
                body.DeploymentConfig.ResourceGroupName,
                body.DeploymentConfig.Location);
        }

        var command = new GenerateArmTemplateFromPromptCommand(
            tenantId, 
            body.Prompt, 
            body.Deploy,
            deploymentConfig);
        var result = await mediator.Send(command);
        return await req.CreateResultResponseAsync(result);
    }
}

public sealed record GenerateArmTemplateFromPromptRequest(
    string Prompt,
    bool Deploy = false,
    DeploymentConfigDto? DeploymentConfig = null);

public sealed record DeploymentConfigDto(
    string SubscriptionId,
    string ResourceGroupName,
    string Location);
