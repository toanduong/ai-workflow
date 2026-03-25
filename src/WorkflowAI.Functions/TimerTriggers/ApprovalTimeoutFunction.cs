using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Approvals.Commands.HandleApprovalTimeout;

namespace WorkflowAI.Functions.TimerTriggers;

public sealed class ApprovalTimeoutFunction(
    IMediator mediator,
    ILogger<ApprovalTimeoutFunction> logger)
{
    [Function("CheckApprovalTimeouts")]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        FunctionContext context)
    {
        logger.LogInformation("Checking for expired approval requests...");

        var result = await mediator.Send(new HandleApprovalTimeoutCommand());

        if (result.IsSuccess)
            logger.LogInformation("Processed {Count} expired approvals.", result.Value);
        else
            logger.LogWarning("Failed to process approval timeouts: {Error}", result.Error?.Message);
    }
}
