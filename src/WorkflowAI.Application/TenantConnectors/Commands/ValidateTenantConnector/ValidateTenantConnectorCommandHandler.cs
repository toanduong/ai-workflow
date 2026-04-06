using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediatR;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;

public sealed class ValidateTenantConnectorCommandHandler(
    ITenantConnectorRepository repository,
    HttpClient httpClient)
    : IRequestHandler<ValidateTenantConnectorCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        ValidateTenantConnectorCommand request, CancellationToken cancellationToken)
    {
        var id = TenantConnectorId.From(request.TenantConnectorId);
        var connector = await repository.GetByIdAsync(id, cancellationToken);

        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound",
                $"TenantConnector {request.TenantConnectorId} not found.");

        try
        {
            var metadata = JsonDocument.Parse(connector.Metadata).RootElement;

            var testEndpoint = ExtractTestEndpoint(metadata);
            if (testEndpoint is null)
            {
                connector.MarkFailed("No testEndpoint found in metadata.");
                await repository.UpdateAsync(connector, cancellationToken);
                return false;
            }

            var authType = metadata.TryGetProperty("authType", out var at)
                ? at.GetString() ?? "APIKey"
                : "APIKey";

            var httpRequest = new HttpRequestMessage(
                new HttpMethod(testEndpoint.Value.Method),
                testEndpoint.Value.Path);

            ApplyCredentials(httpRequest, authType, request.CredentialFields);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                connector.Activate();
                await repository.UpdateAsync(connector, cancellationToken);
                return true;
            }

            var reason = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            connector.MarkFailed(reason);
            await repository.UpdateAsync(connector, cancellationToken);
            return false;
        }
        catch (Exception ex)
        {
            connector.MarkFailed(ex.Message);
            await repository.UpdateAsync(connector, cancellationToken);
            return false;
        }
    }

    /// <summary>
    /// Applies credentials to the HTTP request based on the authType from metadata.
    /// Supports: APIKey, Bearer, Basic, OAuth2, ConnectionString.
    /// Field names come from the metadata's requiredFields / configSchema.
    /// </summary>
    private static void ApplyCredentials(
        HttpRequestMessage request,
        string authType,
        Dictionary<string, string> fields)
    {
        switch (authType)
        {
            case "APIKey":
                // Common API key header names — use whichever field is provided
                if (fields.TryGetValue("api_key", out var apiKey))
                    request.Headers.Add("X-Api-Key", apiKey);
                else if (fields.TryGetValue("apiKey", out var apiKey2))
                    request.Headers.Add("X-Api-Key", apiKey2);
                break;

            case "Bearer":
                if (fields.TryGetValue("token", out var token) ||
                    fields.TryGetValue("access_token", out token) ||
                    fields.TryGetValue("bearer_token", out token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                break;

            case "OAuth2":
                if (fields.TryGetValue("access_token", out var oauthToken) ||
                    fields.TryGetValue("token", out oauthToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);
                }
                break;

            case "Basic":
                if (fields.TryGetValue("username", out var username) &&
                    fields.TryGetValue("password", out var password))
                {
                    var credentials = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes($"{username}:{password}"));
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Basic", credentials);
                }
                break;

            case "ConnectionString":
                // Connection strings aren't sent as headers — just attempt the endpoint
                // The real validation would happen at the DB/service layer
                break;

            default:
                // Fallback: try any field as a header
                foreach (var (key, value) in fields)
                    request.Headers.TryAddWithoutValidation(key, value);
                break;
        }
    }

    private static (string Method, string Path)? ExtractTestEndpoint(JsonElement metadata)
    {
        try
        {
            if (!metadata.TryGetProperty("testEndpoint", out var endpoint))
                return null;

            var method = endpoint.TryGetProperty("method", out var m) ? m.GetString() : "GET";
            var path = endpoint.TryGetProperty("path", out var p) ? p.GetString() : null;

            if (path is null) return null;
            return (method ?? "GET", path);
        }
        catch
        {
            return null;
        }
    }
}
