using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Application.Common.Interfaces;

public interface ILogicAppScriptGenerator
{
    Task<LogicAppGenerationResult> GenerateArmTemplateAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task<LogicAppGenerationResult> GenerateBicepTemplateAsync(Workflow workflow, CancellationToken cancellationToken = default);
}

public sealed record LogicAppGenerationResult(string Content, string FileName, bool Success, string? ErrorMessage = null);
