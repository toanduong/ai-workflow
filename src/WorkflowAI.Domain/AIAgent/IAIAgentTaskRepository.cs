namespace WorkflowAI.Domain.AIAgent;

public interface IAIAgentTaskRepository
{
    Task<AIAgentTask?> GetByIdAsync(AIAgentTaskId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AIAgentTask>> GetByStepExecutionIdAsync(Guid stepExecutionId, CancellationToken cancellationToken = default);
    Task AddAsync(AIAgentTask task, CancellationToken cancellationToken = default);
    Task UpdateAsync(AIAgentTask task, CancellationToken cancellationToken = default);
}
