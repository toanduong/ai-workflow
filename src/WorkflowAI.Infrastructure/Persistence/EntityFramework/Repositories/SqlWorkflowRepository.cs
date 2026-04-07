using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlWorkflowRepository(WorkflowAIDbContext db) : IWorkflowRepository
{
    public async Task<Workflow?> GetByIdAsync(WorkflowId id, CancellationToken cancellationToken = default)
        => await db.Workflows.Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Workflow>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.Workflows.Include(w => w.Steps)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        await db.Workflows.AddAsync(workflow, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        db.Workflows.Update(workflow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(WorkflowId id, CancellationToken cancellationToken = default)
    {
        var workflow = await db.Workflows.FindAsync([id], cancellationToken);
        if (workflow is not null)
        {
            db.Workflows.Remove(workflow);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
