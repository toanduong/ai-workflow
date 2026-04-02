using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlExecutionRepository(WorkflowAIDbContext context) : IExecutionRepository
{
    public async Task<WorkflowExecution?> GetByIdAsync(ExecutionId id, CancellationToken cancellationToken = default)
        => await context.Executions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WorkflowExecution>> GetByWorkflowIdAsync(WorkflowId workflowId, CancellationToken cancellationToken = default)
        => await context.Executions.Where(e => e.WorkflowId == workflowId).ToListAsync(cancellationToken);

    public async Task AddAsync(WorkflowExecution execution, CancellationToken cancellationToken = default)
    {
        context.Executions.Add(execution);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowExecution execution, CancellationToken cancellationToken = default)
    {
        context.Executions.Update(execution);
        await context.SaveChangesAsync(cancellationToken);
    }
}
