using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Persistence.BlobStorage.Repositories;

internal sealed class BlobWorkflowRepository(BlobStorageContext context)
    : BlobRepositoryBase(context.Workflows), IWorkflowRepository
{
    public Task<Workflow?> GetByIdAsync(WorkflowId id, CancellationToken ct = default)
        => GetAsync<Workflow>(id.Value, ct);

    public Task<IReadOnlyList<Workflow>> GetAllAsync(CancellationToken ct = default)
        => GetAllAsync<Workflow>(ct);

    public Task AddAsync(Workflow workflow, CancellationToken ct = default)
        => SaveAsync(workflow.Id.Value, workflow, overwrite: false, ct);

    public Task UpdateAsync(Workflow workflow, CancellationToken ct = default)
        => SaveAsync(workflow.Id.Value, workflow, overwrite: true, ct);

    public Task DeleteAsync(WorkflowId id, CancellationToken ct = default)
        => DeleteAsync(id.Value, ct);
}
