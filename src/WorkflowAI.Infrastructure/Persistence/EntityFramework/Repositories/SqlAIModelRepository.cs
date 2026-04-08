using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.AIAgent;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlAIModelRepository(WorkflowAIDbContext context) : IAIModelRepository
{
    public async Task<IReadOnlyList<AIModel>> GetAllAsync(CancellationToken ct = default)
        => await context.AIModels.OrderBy(m => m.Provider).ThenBy(m => m.DisplayName).ToListAsync(ct);

    public async Task<AIModel?> GetByModelIdAsync(string modelId, CancellationToken ct = default)
        => await context.AIModels.FirstOrDefaultAsync(m => m.ModelId == modelId, ct);

    public async Task<AIModel?> GetDefaultAsync(CancellationToken ct = default)
        => await context.AIModels.FirstOrDefaultAsync(m => m.IsDefault && m.IsEnabled, ct);

    public async Task AddAsync(AIModel model, CancellationToken ct = default)
    {
        context.AIModels.Add(model);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AIModel model, CancellationToken ct = default)
    {
        context.AIModels.Update(model);
        await context.SaveChangesAsync(ct);
    }
}
