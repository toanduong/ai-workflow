using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.LogicApps;

public sealed class StepToConnectorMapper
{
    public string MapToLogicAppActionType(StepType stepType) => stepType.Name switch
    {
        nameof(StepType.AIAgent) => "Http",
        nameof(StepType.HumanApproval) => "HttpWebhook",
        nameof(StepType.Notification) => "ApiConnection",
        nameof(StepType.Action) => "Http",
        _ => "Http"
    };
}
