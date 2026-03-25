using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace WorkflowAI.Application.Common.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int WarningThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > WarningThresholdMs)
        {
            logger.LogWarning(
                "Long running request: {RequestName} ({ElapsedMs}ms) {@Request}",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds,
                request);
        }

        return response;
    }
}
