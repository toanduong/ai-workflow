using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.ExecuteConnectorApi;

/// <summary>
/// Proxies a call to a 3rd-party API using stored credentials.
/// Works dynamically for any connector — Apollo, Salesforce, HubSpot, Shopify, etc.
/// </summary>
public sealed class ExecuteConnectorApiCommandHandler(
    ITenantConnectorRepository repository,
    IKeyVaultService keyVaultService,
    ICredentialApplicatorFactory applicatorFactory,
    ILogger<ExecuteConnectorApiCommandHandler> logger,
    HttpClient httpClient)
    : IRequestHandler<ExecuteConnectorApiCommand, Result<JsonElement>>
{
    public async Task<Result<JsonElement>> Handle(
        ExecuteConnectorApiCommand request, CancellationToken ct)
    {
        // 1. Load connector and verify it belongs to the tenant and is active
        var connector = await repository.GetByIdAsync(
            TenantConnectorId.From(request.TenantConnectorId), ct);

        if (connector is null || connector.TenantId.Value != request.TenantId)
            return Error.NotFound("TenantConnector.NotFound", "Connector not found.");

        if (connector.Status != TenantConnectorStatus.Active)
            return Error.Validation("TenantConnector.NotActive",
                $"Connector is not active (status: {connector.Status.Name}).");

        if (string.IsNullOrEmpty(connector.CredentialSecretNames))
            return Error.Validation("TenantConnector.NoCredentials",
                "Connector has no stored credentials. Please validate the connector first.");

        // 2. Load the specific API definition
        var apis = await repository.GetApisByConnectorAsync(
            TenantConnectorId.From(request.TenantConnectorId), ct);

        var api = apis.FirstOrDefault(a => a.Id.Value == request.ApiId);
        if (api is null)
            return Error.NotFound("ConnectorApi.NotFound", "API not found for this connector.");

        // 3. Resolve credentials from Key Vault
        var credentials = await ResolveCredentialsAsync(connector, ct);

        // 4. Build the HTTP request
        var url = ResolveUrl(api.UrlTemplate, credentials, request.RequestBody);
        var httpRequest = new HttpRequestMessage(new HttpMethod(api.HttpMethod), url);

        // 5. Apply auth headers dynamically based on connector's authType
        var authType = ExtractAuthType(connector.Metadata);
        var applicator = applicatorFactory.Resolve(authType);
        applicator.Apply(httpRequest, credentials);

        // 6. Attach request body for POST/PUT/PATCH
        if (request.RequestBody.HasValue &&
            request.RequestBody.Value.ValueKind != JsonValueKind.Null &&
            api.HttpMethod is "POST" or "PUT" or "PATCH")
        {
            httpRequest.Content = new StringContent(
                request.RequestBody.Value.GetRawText(),
                Encoding.UTF8,
                "application/json");
        }

        // 7. Execute and return response
        try
        {
            logger.LogInformation(
                "Executing {Method} {Url} for connector {ConnectorType}",
                api.HttpMethod, url, connector.ConnectorType);

            var response = await httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "3rd party API returned {StatusCode} for {ConnectorType}/{ApiName}",
                    (int)response.StatusCode, connector.ConnectorType, api.ApiName);

                return Error.Validation("ConnectorApi.Failed",
                    $"3rd party API returned {(int)response.StatusCode}: {responseBody}");
            }

            var json = string.IsNullOrWhiteSpace(responseBody)
                ? JsonDocument.Parse("{}").RootElement
                : JsonDocument.Parse(responseBody).RootElement;

            return Result.Success(json);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling {ConnectorType}/{ApiName}", connector.ConnectorType, api.ApiName);
            return Error.Validation("ConnectorApi.HttpError", ex.Message);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse response from {ConnectorType}/{ApiName}", connector.ConnectorType, api.ApiName);
            return Error.Validation("ConnectorApi.ParseError", "Failed to parse 3rd party API response.");
        }
    }

    /// <summary>
    /// Fetches all stored credentials from Key Vault using the connector's secret name mapping.
    /// </summary>
    private async Task<Dictionary<string, string>> ResolveCredentialsAsync(
        TenantConnector connector, CancellationToken ct)
    {
        var result = new Dictionary<string, string>();

        try
        {
            var secretNames = JsonSerializer.Deserialize<Dictionary<string, string>>(
                connector.CredentialSecretNames!) ?? new();

            foreach (var (field, secretName) in secretNames)
            {
                try
                {
                    var value = await keyVaultService.GetSecretAsync(secretName, ct: ct);
                    result[field] = value;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not fetch secret {SecretName} for field {Field}", secretName, field);
                }
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse CredentialSecretNames for connector {Id}", connector.Id.Value);
        }

        return result;
    }

    /// <summary>
    /// Substitutes {placeholder} values in the URL template using credentials or request body fields.
    /// e.g. "https://{instance_url}/api/contacts/{id}" → "https://mycrm.com/api/contacts/123"
    /// </summary>
    private static string ResolveUrl(
        string urlTemplate,
        Dictionary<string, string> credentials,
        JsonElement? requestBody)
    {
        var url = urlTemplate;

        // Substitute from credentials first
        foreach (var (key, value) in credentials)
            url = url.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);

        // Substitute remaining placeholders from request body
        if (requestBody.HasValue && requestBody.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in requestBody.Value.EnumerateObject())
            {
                var strVal = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.GetRawText();
                url = url.Replace($"{{{prop.Name}}}", strVal, StringComparison.OrdinalIgnoreCase);
            }
        }

        return url;
    }

    private static string ExtractAuthType(string metadata)
    {
        try
        {
            var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.TryGetProperty("authType", out var at))
                return at.GetString() ?? "APIKey";
        }
        catch (JsonException) { }
        return "APIKey";
    }
}
