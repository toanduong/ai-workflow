using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;

namespace WorkflowAI.Infrastructure.Connectors;

public sealed class ConnectorHttpValidator(
    IHttpClientFactory httpClientFactory,
    ICredentialApplicatorFactory credentialApplicatorFactory,
    ILogger<ConnectorHttpValidator> logger) : IConnectorHttpValidator
{
    public async Task<(bool IsValid, string? FailureReason)> TestAsync(
        string url,
        string httpMethod,
        string authType,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var request = new HttpRequestMessage(new HttpMethod(httpMethod), url);
            credentialApplicatorFactory.Resolve(authType).Apply(request, credentials);

            logger.LogInformation("Testing connector: {Method} {Url} (authType: {AuthType})",
                httpMethod, url, authType);

            var response = await client.SendAsync(request, ct);

            logger.LogInformation("Connector test response: {StatusCode} {Reason}",
                (int)response.StatusCode, response.ReasonPhrase);

            if (response.IsSuccessStatusCode)
                return (true, null);

            return (false, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase} — URL: {url}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Connection test failed: {Method} {Url}", httpMethod, url);
            return (false, $"{ex.Message} — URL: {url}");
        }
    }
}
