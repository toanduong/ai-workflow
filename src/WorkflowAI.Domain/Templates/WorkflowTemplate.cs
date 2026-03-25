using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Templates;

public sealed class WorkflowTemplate : Entity<TemplateId>
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public string? DefaultSteps { get; private set; }
    public bool IsActive { get; private set; } = true;

    private WorkflowTemplate() { }

    public static WorkflowTemplate Create(string name, string? description, string? category, string? defaultSteps)
    {
        return new WorkflowTemplate
        {
            Id = TemplateId.New(),
            Name = name,
            Description = description,
            Category = category,
            DefaultSteps = defaultSteps
        };
    }

    public void Update(string name, string? description, string? category, string? defaultSteps)
    {
        Name = name;
        Description = description;
        Category = category;
        DefaultSteps = defaultSteps;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
