using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.Channels;
using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Templates;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework;

public sealed class WorkflowAIDbContext(DbContextOptions<WorkflowAIDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkflowTemplate> Templates => Set<WorkflowTemplate>();
    public DbSet<NotificationChannel> Channels => Set<NotificationChannel>();
    public DbSet<Connector> Connectors => Set<Connector>();
    public DbSet<ConnectorCredential> ConnectorCredentials => Set<ConnectorCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowAIDbContext).Assembly);
    }
}
