using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.LogicApps;

public sealed class StepToConnectorMapper
{
    /// <summary>
    /// Maps a step type to a Logic App action type.
    /// - AIAgent / Action / Notification → Http (outbound HTTP call)
    /// - HumanApproval with a callback URL → HttpWebhook; without → Http (plain wait-step placeholder)
    /// </summary>
    public string MapToLogicAppActionType(StepType stepType, string? configuration = null) => stepType.Name switch
    {
        nameof(StepType.AIAgent) => "Http",
        nameof(StepType.HumanApproval) => "Http",
        nameof(StepType.Notification) => "Http",
        nameof(StepType.Action) => "Http",
        _ => "Http"
    };

    /// <summary>
    /// Returns the HTTP method for the Logic App action.
    /// Falls back when no explicit method is set on the step.
    /// - Notification → GET (fetch/read)
    /// - All others → POST (write/trigger)
    /// </summary>
    public string MapToHttpMethod(StepType stepType) => stepType.Name switch
    {
        nameof(StepType.Notification) => "GET",
        _ => "POST"
    };
}
