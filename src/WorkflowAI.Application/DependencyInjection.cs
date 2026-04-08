using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using WorkflowAI.Application.Common.Behaviors;
using WorkflowAI.Application.TenantConnectors.Commands.GenerateConnectorAssets;

namespace WorkflowAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        // Configure connector asset generation options
        services.Configure<ConnectorAssetGenerationOptions>(options => { });

        return services;
    }
}
