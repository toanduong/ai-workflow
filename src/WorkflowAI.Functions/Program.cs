using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkflowAI.Application;
using WorkflowAI.Functions;
using WorkflowAI.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.UseMiddleware<DevAuthMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);

        services.AddApplicationInsightsTelemetryWorkerService();
    })
    .Build();

host.Run();
