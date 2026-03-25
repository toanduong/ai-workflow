namespace WorkflowAI.Domain.Workflows;

public interface IWorkflowRepository
{
    Task<Workflow?> GetByIdAsync(WorkflowId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Workflow>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task UpdateAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task DeleteAsync(WorkflowId id, CancellationToken cancellationToken = default);
}
