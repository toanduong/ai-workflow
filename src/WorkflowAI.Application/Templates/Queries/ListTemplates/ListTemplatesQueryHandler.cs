using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Templates;

namespace WorkflowAI.Application.Templates.Queries.ListTemplates;

public sealed class ListTemplatesQueryHandler(ITemplateRepository templateRepository)
    : IRequestHandler<ListTemplatesQuery, Result<IReadOnlyList<TemplateDto>>>
{
    public async Task<Result<IReadOnlyList<TemplateDto>>> Handle(ListTemplatesQuery request, CancellationToken ct)
    {
        var templates = await templateRepository.GetActiveAsync(ct);
        var dtos = templates.Select(t => new TemplateDto(
            t.Id.Value, t.Name, t.Description, t.Category, t.IsActive, t.CreatedAt))
            .ToList().AsReadOnly();
        return Result<IReadOnlyList<TemplateDto>>.Success(dtos);
    }
}
