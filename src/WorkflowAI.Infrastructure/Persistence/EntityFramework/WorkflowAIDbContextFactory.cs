using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework;

public sealed class WorkflowAIDbContextFactory : IDesignTimeDbContextFactory<WorkflowAIDbContext>
{
    public WorkflowAIDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkflowAIDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=workflow-ai;Username=postgres;Password=LeVanLap12#10");
        return new WorkflowAIDbContext(optionsBuilder.Options);
    }
}
