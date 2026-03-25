using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Domain.Executions;

public interface IExecutionRepository
{
    Task<WorkflowExecution?> GetByIdAsync(ExecutionId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowExecution>> GetByWorkflowIdAsync(WorkflowId workflowId, CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowExecution execution, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkflowExecution execution, CancellationToken cancellationToken = default);
}
