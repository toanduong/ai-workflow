using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.AIAgent;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlAIAgentTaskRepository(WorkflowAIDbContext context) : IAIAgentTaskRepository
{
    public async Task<AIAgentTask?> GetByIdAsync(AIAgentTaskId id, CancellationToken cancellationToken = default)
        => await context.AIAgentTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AIAgentTask>> GetByStepExecutionIdAsync(Guid stepExecutionId, CancellationToken cancellationToken = default)
        => await context.AIAgentTasks
            .Where(t => t.StepExecutionId == stepExecutionId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(AIAgentTask task, CancellationToken cancellationToken = default)
    {
        context.AIAgentTasks.Add(task);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AIAgentTask task, CancellationToken cancellationToken = default)
    {
        context.AIAgentTasks.Update(task);
        await context.SaveChangesAsync(cancellationToken);
    }
}
