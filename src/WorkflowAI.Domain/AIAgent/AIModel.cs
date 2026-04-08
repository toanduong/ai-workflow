using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.AIAgent;

public sealed class AIModel : Entity<AIModelId>
{
    /// <summary>The model identifier used in API calls (e.g. "claude-sonnet-4-6").</summary>
    public string ModelId { get; private set; } = string.Empty;

    /// <summary>Provider name: Anthropic, OpenAI, AzureOpenAI, Google.</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>Human-readable name (e.g. "Claude Sonnet 4.6").</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>USD cost per 1K input tokens.</summary>
    public decimal InputCostPer1KTokens { get; private set; }

    /// <summary>USD cost per 1K output tokens.</summary>
    public decimal OutputCostPer1KTokens { get; private set; }

    /// <summary>Maximum context window in tokens (input + output).</summary>
    public int ContextWindow { get; private set; }

    /// <summary>When false, this model is excluded from selection.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>The global fallback model. At most one row should have this set to true.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>Optional notes about capabilities or recommended use cases.</summary>
    public string? Notes { get; private set; }

    private AIModel() { }

    public static AIModel Create(
        string modelId,
        string provider,
        string displayName,
        decimal inputCostPer1KTokens,
        decimal outputCostPer1KTokens,
        int contextWindow,
        bool isEnabled = true,
        bool isDefault = false,
        string? notes = null)
    {
        return new AIModel
        {
            Id = AIModelId.New(),
            ModelId = modelId,
            Provider = provider,
            DisplayName = displayName,
            InputCostPer1KTokens = inputCostPer1KTokens,
            OutputCostPer1KTokens = outputCostPer1KTokens,
            ContextWindow = contextWindow,
            IsEnabled = isEnabled,
            IsDefault = isDefault,
            Notes = notes
        };
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
