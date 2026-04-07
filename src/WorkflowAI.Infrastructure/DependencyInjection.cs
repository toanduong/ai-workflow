using Anthropic.SDK;
using Azure.Communication.Email;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.ResourceManager;
using Microsoft.Extensions.AI;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.AIAgent;
using WorkflowAI.Domain.Approvals;
using WorkflowAI.Domain.Channels;
using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Notifications;
using WorkflowAI.Domain.Templates;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Domain.Users;
using WorkflowAI.Domain.Workflows;
using WorkflowAI.Infrastructure.AI;
using WorkflowAI.Infrastructure.Connectors.Auth;
using WorkflowAI.Infrastructure.Connectors;
using WorkflowAI.Infrastructure.Identity;
using WorkflowAI.Infrastructure.LogicApps;
using WorkflowAI.Infrastructure.Messaging;
using WorkflowAI.Infrastructure.Messaging.ServiceBus;
using WorkflowAI.Infrastructure.Notifications;
using WorkflowAI.Infrastructure.Notifications.Adapters;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;
using WorkflowAI.Infrastructure.Services;
using WorkflowAI.Infrastructure.Storage;

namespace WorkflowAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Cosmos DB (Deprecated - no longer used)
        //services.AddScoped<IExecutionRepository, CosmosExecutionRepository>();
        //services.AddScoped<IApprovalRepository, CosmosApprovalRepository>();
        //services.AddScoped<INotificationRepository, CosmosNotificationRepository>();
        //services.AddScoped<IAIAgentTaskRepository, CosmosAIAgentTaskRepository>();

        // EF Core (PostgreSQL)
        services.AddDbContext<WorkflowAIDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PostgreSql")));

        services.AddScoped<IUserRepository, SqlUserRepository>();
        services.AddScoped<ITemplateRepository, SqlTemplateRepository>();
        services.AddScoped<IChannelRepository, SqlChannelRepository>();
        services.AddScoped<IExecutionRepository, SqlExecutionRepository>();
        services.AddScoped<IApprovalRepository, SqlApprovalRepository>();
        services.AddScoped<INotificationRepository, SqlNotificationRepository>();
        services.AddScoped<IAIAgentTaskRepository, SqlAIAgentTaskRepository>();

        // Service Bus
        services.AddSingleton(sp =>
            new ServiceBusClient(configuration.GetConnectionString("ServiceBus")));
        services.AddScoped<IServiceBusPublisher, ServiceBusPublisher>();

        // Domain Event Dispatcher
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Azure OpenAI
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));
        services.AddScoped<IAzureOpenAIService, AzureOpenAIService>();

        // Anthropic Claude
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.AddSingleton<IChatClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
            return new AnthropicClient(apiKeys: new APIAuthentication(opts.ApiKey)).Messages;
        });
        services.AddScoped<IAnthropicService, AnthropicService>();

        // Notification Adapters
        services.AddHttpClient<SlackNotificationAdapter>();
        services.AddHttpClient<TeamsNotificationAdapter>();
        services.AddSingleton(sp =>
            new EmailClient(configuration.GetConnectionString("AzureCommunicationServices")));
        services.AddScoped<EmailNotificationAdapter>();
        services.AddScoped<INotificationSender, NotificationRouter>();

        // Blob Storage
        services.AddSingleton(sp =>
            new BlobServiceClient(configuration.GetConnectionString("BlobStorage")));
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IWorkflowRepository, BlobWorkflowRepository>();

        // Identity & Token
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        var tokenSigningKey = configuration["Security:ApprovalTokenSigningKey"] ?? "default-dev-key-change-in-prod";
        services.AddSingleton<ITokenService>(new TokenService(tokenSigningKey));

        // Services
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // Credential applicators (Strategy pattern)
        services.AddScoped<ICredentialApplicator, ApiKeyCredentialApplicator>();
        services.AddScoped<ICredentialApplicator, BearerCredentialApplicator>();
        services.AddScoped<ICredentialApplicator, OAuth2CredentialApplicator>();
        services.AddScoped<ICredentialApplicator, BasicCredentialApplicator>();
        services.AddScoped<ICredentialApplicator, DefaultCredentialApplicator>();
        services.AddScoped<ICredentialApplicatorFactory, CredentialApplicatorFactory>();

        // Connector services
        services.AddScoped<IConnectorRepository, SqlConnectorRepository>();
        services.AddScoped<IConnectorService, ConnectorService>();
        services.AddScoped<IApiConnectionProvisioner, ApiConnectionProvisioner>();
        services.AddScoped<IConnectorCredentialResolver, ConnectorCredentialResolver>();
        services.AddScoped<ITenantConnectorRepository, SqlTenantConnectorRepository>();
        services.AddScoped<IConnectorHttpValidator, ConnectorHttpValidator>();

        // HTTP client for HttpStep execution
        services.AddHttpClient<WorkflowAI.Application.Executions.Commands.ExecuteHttpStep.ExecuteHttpStepCommandHandler>();

        // Key Vault
        var keyVaultUri = configuration["KeyVault:Uri"];
        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            services.AddSingleton(new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential()));
            services.AddScoped<IKeyVaultService, KeyVaultService>();
        }
        else
        {
            services.AddScoped<IKeyVaultService, NoOpKeyVaultService>();
        }

        // HTTP client for ValidateTenantConnector
        services.AddHttpClient<WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector.ValidateTenantConnectorCommandHandler>();

        // Logic App Generator + Deployer
        services.AddSingleton<StepToConnectorMapper>();
        services.AddScoped<ILogicAppScriptGenerator, LogicAppScriptGenerator>();
        services.Configure<AzureResourcesOptions>(configuration.GetSection(AzureResourcesOptions.SectionName));
        services.AddSingleton(new ArmClient(new DefaultAzureCredential()));
        services.AddScoped<ILogicAppDeployer, LogicAppDeployer>();

        return services;
    }
}
