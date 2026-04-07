namespace WorkflowAI.Application.Common.Interfaces;

/// <summary>
/// Tests a connector's reachability using the test endpoint from Claude-generated Metadata.
/// Auth is applied generically via authType + credentials — no hardcoded auth schemes.
/// </summary>
public interface IConnectorHttpValidator
{
    Task<(bool IsValid, string? FailureReason)> TestAsync(
        string url,
        string httpMethod,
        string authType,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken ct = default);
}
