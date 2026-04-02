using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Users;
using WorkflowAI.Infrastructure.Identity;

namespace WorkflowAI.Functions;

/// <summary>
/// Dev-only middleware: reads X-User-Id header and populates CurrentUserService.
/// In production, replace with real JWT/token validation.
/// </summary>
public sealed class DevAuthMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = await context.GetHttpRequestDataAsync();
        if (httpContext is not null)
        {
            var currentUserService = context.InstanceServices.GetService(typeof(ICurrentUserService)) as CurrentUserService;
            if (currentUserService is not null)
            {
                if (httpContext.Headers.TryGetValues("X-User-Id", out var userIdValues))
                {
                    var userIdStr = userIdValues.FirstOrDefault();
                    if (Guid.TryParse(userIdStr, out var userGuid))
                    {
                        currentUserService.UserId = UserId.From(userGuid);
                        currentUserService.DisplayName = httpContext.Headers.TryGetValues("X-User-Name", out var nameValues)
                            ? nameValues.FirstOrDefault()
                            : "Dev User";
                        currentUserService.Email = httpContext.Headers.TryGetValues("X-User-Email", out var emailValues)
                            ? emailValues.FirstOrDefault()
                            : "dev@localhost";
                        currentUserService.ExternalId = userIdStr;
                    }
                }
            }
        }

        await next(context);
    }
}
