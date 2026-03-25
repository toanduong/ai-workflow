using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.AIAgent;

public sealed class AIAgentTask : Entity<AIAgentTaskId>
{
    public Guid StepExecutionId { get; private set; }
    public string PromptTemplate { get; private set; } = string.Empty;
    public string? PromptVariables { get; private set; }
    public string? LLMResponse { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public int TokensUsed { get; private set; }
    public AITaskStatus Status { get; private set; } = AITaskStatus.Pending;
    public DateTime? CompletedAt { get; private set; }

    private AIAgentTask() { }

    public static AIAgentTask Create(Guid stepExecutionId, string promptTemplate, string? promptVariables, string model)
    {
        return new AIAgentTask
        {
            Id = AIAgentTaskId.New(),
            StepExecutionId = stepExecutionId,
            PromptTemplate = promptTemplate,
            PromptVariables = promptVariables,
            Model = model,
            Status = AITaskStatus.Pending
        };
    }

    public void StartProcessing()
    {
        Status = AITaskStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(string llmResponse, int tokensUsed)
    {
        LLMResponse = llmResponse;
        TokensUsed = tokensUsed;
        Status = AITaskStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Fail()
    {
        Status = AITaskStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
