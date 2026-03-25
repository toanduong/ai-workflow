using MediatR;
using WorkflowAI.Domain.Common;

namespace WorkflowAI.Application.Templates.Queries.ListTemplates;

public sealed record ListTemplatesQuery() : IRequest<Result<IReadOnlyList<TemplateDto>>>;
