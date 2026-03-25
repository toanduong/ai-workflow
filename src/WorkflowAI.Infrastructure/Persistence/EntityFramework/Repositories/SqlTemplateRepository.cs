using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.Templates;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlTemplateRepository(WorkflowAIDbContext context) : ITemplateRepository
{
    public async Task<WorkflowTemplate?> GetByIdAsync(TemplateId id, CancellationToken cancellationToken = default)
        => await context.Templates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WorkflowTemplate>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await context.Templates.Where(t => t.IsActive).ToListAsync(cancellationToken);

    public async Task AddAsync(WorkflowTemplate template, CancellationToken cancellationToken = default)
    {
        context.Templates.Add(template);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowTemplate template, CancellationToken cancellationToken = default)
    {
        context.Templates.Update(template);
        await context.SaveChangesAsync(cancellationToken);
    }
}
