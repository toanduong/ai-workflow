using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Persistence.BlobStorage.Repositories;

internal sealed class BlobExecutionRepository(BlobStorageContext context)
    : BlobRepositoryBase(context.Executions), IExecutionRepository
{
    public Task<WorkflowExecution?> GetByIdAsync(ExecutionId id, CancellationToken ct = default)
        => GetAsync<WorkflowExecution>(id.Value, ct);

    public async Task<IReadOnlyList<WorkflowExecution>> GetByWorkflowIdAsync(WorkflowId workflowId, CancellationToken ct = default)
    {
        var all = await GetAllAsync<WorkflowExecution>(ct);
        return all.Where(e => e.WorkflowId == workflowId).ToList().AsReadOnly();
    }

    public Task AddAsync(WorkflowExecution execution, CancellationToken ct = default)
        => SaveAsync(execution.Id.Value, execution, overwrite: false, ct);

    public Task UpdateAsync(WorkflowExecution execution, CancellationToken ct = default)
        => SaveAsync(execution.Id.Value, execution, overwrite: true, ct);
}
