using WorkflowAI.Domain.AIAgent;

namespace WorkflowAI.Infrastructure.Persistence.BlobStorage.Repositories;

internal sealed class BlobAIAgentTaskRepository(BlobStorageContext context)
    : BlobRepositoryBase(context.AITasks), IAIAgentTaskRepository
{
    public Task<AIAgentTask?> GetByIdAsync(AIAgentTaskId id, CancellationToken ct = default)
        => GetAsync<AIAgentTask>(id.Value, ct);

    public async Task<IReadOnlyList<AIAgentTask>> GetByStepExecutionIdAsync(Guid stepExecutionId, CancellationToken ct = default)
    {
        var all = await GetAllAsync<AIAgentTask>(ct);
        return all.Where(t => t.StepExecutionId == stepExecutionId).ToList().AsReadOnly();
    }

    public Task AddAsync(AIAgentTask task, CancellationToken ct = default)
        => SaveAsync(task.Id.Value, task, overwrite: false, ct);

    public Task UpdateAsync(AIAgentTask task, CancellationToken ct = default)
        => SaveAsync(task.Id.Value, task, overwrite: true, ct);
}
