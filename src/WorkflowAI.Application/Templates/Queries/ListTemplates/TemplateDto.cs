namespace WorkflowAI.Application.Templates.Queries.ListTemplates;

public sealed record TemplateDto(
    Guid Id, string Name, string? Description, string? Category,
    bool IsActive, DateTime CreatedAt);
