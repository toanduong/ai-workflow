using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework;

/// <summary>
/// Used by EF Core tooling (migrations). Reads connection string from the
/// WORKFLOWAI_CONNECTION_STRING environment variable, falling back to the
/// default local dev connection string so migrations work out of the box
/// after running docker-compose up.
/// </summary>
public sealed class WorkflowAIDbContextFactory : IDesignTimeDbContextFactory<WorkflowAIDbContext>
{
    private const string DefaultLocalConnectionString =
        "Host=localhost;Port=5433;Database=workflowai_dev;Username=postgres;Password=devpassword;Ssl Mode=Disable";

    public WorkflowAIDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("WORKFLOWAI_CONNECTION_STRING")
            ?? DefaultLocalConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<WorkflowAIDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new WorkflowAIDbContext(optionsBuilder.Options);
    }
}
