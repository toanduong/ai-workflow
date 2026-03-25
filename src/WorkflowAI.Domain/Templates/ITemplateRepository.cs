namespace WorkflowAI.Domain.Templates;

public interface ITemplateRepository
{
    Task<WorkflowTemplate?> GetByIdAsync(TemplateId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTemplate>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowTemplate template, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkflowTemplate template, CancellationToken cancellationToken = default);
}
