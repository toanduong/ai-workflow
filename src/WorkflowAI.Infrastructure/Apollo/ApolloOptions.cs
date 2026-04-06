namespace WorkflowAI.Infrastructure.Apollo;

public sealed class ApolloOptions
{
    public const string SectionName = "Apollo";

    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.apollo.io/v1";
}
