namespace WorkflowAI.Infrastructure.LogicApps;

public sealed class AzureResourcesOptions
{
    public const string SectionName = "AzureResources";

    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public bool DeployEnabled { get; set; } = false;
}
