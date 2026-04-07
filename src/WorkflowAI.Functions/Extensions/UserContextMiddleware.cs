using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Functions.Extensions;

public sealed class UserContextMiddleware(ICurrentUserService currentUserService) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var requestData = await context.GetHttpRequestDataAsync();
        if (requestData is not null)
        {
            if (requestData.Headers.TryGetValues("X-User-Id", out var userIdValues) &&
                Guid.TryParse(userIdValues.FirstOrDefault(), out var userId))
            {
                var service = (WorkflowAI.Infrastructure.Identity.CurrentUserService)currentUserService;
                service.UserId = UserId.From(userId);
                service.DisplayName = requestData.Headers.TryGetValues("X-User-Name", out var names)
                    ? names.FirstOrDefault()
                    : "Dev User";
                service.Email = requestData.Headers.TryGetValues("X-User-Email", out var emails)
                    ? emails.FirstOrDefault()
                    : null;
            }
        }

        await next(context);
    }
}
