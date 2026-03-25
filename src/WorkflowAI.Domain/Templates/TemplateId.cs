namespace WorkflowAI.Domain.Templates;

public readonly record struct TemplateId(Guid Value)
{
    public static TemplateId New() => new(Guid.NewGuid());
    public static TemplateId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
