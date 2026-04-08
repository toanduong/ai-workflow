using Microsoft.EntityFrameworkCore;
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

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework;

public sealed class WorkflowAIDbContext(DbContextOptions<WorkflowAIDbContext> options) : DbContext(options)
{
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkflowTemplate> Templates => Set<WorkflowTemplate>();
    public DbSet<NotificationChannel> Channels => Set<NotificationChannel>();
    public DbSet<Connector> Connectors => Set<Connector>();
    public DbSet<ConnectorCredential> ConnectorCredentials => Set<ConnectorCredential>();
    public DbSet<WorkflowExecution> Executions => Set<WorkflowExecution>();
    public DbSet<StepExecution> StepExecutions => Set<StepExecution>();
    public DbSet<ApprovalRequest> Approvals => Set<ApprovalRequest>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AIAgentTask> AIAgentTasks => Set<AIAgentTask>();
    public DbSet<TenantConnector> TenantConnectors => Set<TenantConnector>();
    public DbSet<TenantConnectorApi> TenantConnectorApis => Set<TenantConnectorApi>();
    public DbSet<TenantConnectorApiHealthCheck> TenantConnectorApiHealthChecks => Set<TenantConnectorApiHealthCheck>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowAIDbContext).Assembly);
    }
}
