using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework;

public sealed class WorkflowAIDbContextFactory : IDesignTimeDbContextFactory<WorkflowAIDbContext>
{
    public WorkflowAIDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkflowAIDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=workflowai_dev;Username=postgres;Password=devpassword");
        return new WorkflowAIDbContext(optionsBuilder.Options);
    }
}
