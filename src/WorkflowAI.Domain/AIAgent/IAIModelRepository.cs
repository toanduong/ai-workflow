namespace WorkflowAI.Domain.AIAgent;

public interface IAIModelRepository
{
    Task<IReadOnlyList<AIModel>> GetAllAsync(CancellationToken ct = default);
    Task<AIModel?> GetByModelIdAsync(string modelId, CancellationToken ct = default);
    Task<AIModel?> GetDefaultAsync(CancellationToken ct = default);
    Task AddAsync(AIModel model, CancellationToken ct = default);
    Task UpdateAsync(AIModel model, CancellationToken ct = default);
}
