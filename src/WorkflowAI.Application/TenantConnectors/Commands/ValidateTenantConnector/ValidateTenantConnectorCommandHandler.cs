using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;

public sealed class ValidateTenantConnectorCommandHandler(
    ITenantConnectorRepository repository,
    ICredentialApplicatorFactory applicatorFactory,
    IKeyVaultService keyVaultService,
    ICurrentUserService currentUserService,
    ILogger<ValidateTenantConnectorCommandHandler> logger,
    HttpClient httpClient)
    : IRequestHandler<ValidateTenantConnectorCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        ValidateTenantConnectorCommand request, CancellationToken ct)
    {
        if (!currentUserService.IsAuthenticated)
            return Error.Unauthorized("Auth.Required", "Authentication is required.");

        var connector = await repository.GetByIdAsync(
            TenantConnectorId.From(request.TenantConnectorId), ct);

        if (connector is null)
            return Error.NotFound("TenantConnector.NotFound", "Connector not found.");

        if (connector.TenantId.Value != request.TenantId)
            return Error.NotFound("TenantConnector.NotFound", "Connector not found.");

        var baseUrl = ResolveBaseUrl(connector.Metadata, request.Credentials);
        var (testUrl, testMethod, testBody, testContentType) = ExtractTestEndpoint(connector.Metadata, baseUrl);

        if (string.IsNullOrEmpty(testUrl))
        {
            connector.MarkFailed("Missing testEndpoint in connector metadata.");
            await repository.UpdateAsync(connector, ct);
            return false;
        }

        var authType = ExtractAuthType(connector.Metadata);
        var applicator = applicatorFactory.Resolve(authType);

        HttpResponseMessage response;
        try
        {
            var httpRequest = new HttpRequestMessage(new HttpMethod(testMethod), testUrl);
            applicator.Apply(httpRequest, request.Credentials);

            if (testBody is not null)
                httpRequest.Content = new StringContent(
                    testBody,
                    System.Text.Encoding.UTF8,
                    testContentType ?? "application/json");

            response = await httpClient.SendAsync(httpRequest, ct);
        }
        catch (HttpRequestException ex)
        {
            connector.MarkFailed(ex.Message);
            await repository.UpdateAsync(connector, ct);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            connector.MarkFailed($"HTTP {(int)response.StatusCode}");
            await repository.UpdateAsync(connector, ct);
            return false;
        }

        var secretNames = await StoreCredentialsAsync(connector, request.Credentials, ct);
        connector.Activate(secretNames);
        await repository.UpdateAsync(connector, ct);

        logger.LogInformation("Connector {ConnectorName} validated successfully", connector.ConnectorName);
        return true;
    }

    /// <summary>
    /// Resolves the base URL for the connector.
    ///
    /// Claude may generate one of two patterns in Metadata.baseUrl:
    ///   - Fixed URL   → "https://api.apollo.io"       (SaaS, same for all tenants)
    ///   - Template    → "{instance_url}"               (self-hosted, differs per tenant)
    ///
    /// Template placeholders are substituted from the credentials dict at runtime.
    /// </summary>
    private static string ResolveBaseUrl(string metadata, IReadOnlyDictionary<string, string> credentials)
    {
        try
        {
            var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.TryGetProperty("baseUrl", out var bu))
            {
                var baseUrl = bu.GetString() ?? string.Empty;

                foreach (var (key, value) in credentials)
                    baseUrl = baseUrl.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);

                if (baseUrl.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
                    baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return baseUrl.TrimEnd('/');
            }
        }
        catch (JsonException) { }

        return credentials.Values
            .FirstOrDefault(v =>
                v.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                v.StartsWith("http://",  StringComparison.OrdinalIgnoreCase))
            ?.TrimEnd('/') ?? string.Empty;
    }

    private static (string Url, string Method, string? Body, string? ContentType) ExtractTestEndpoint(
        string metadata, string baseUrl)
    {
        try
        {
            var doc = JsonDocument.Parse(metadata);
            if (doc.RootElement.TryGetProperty("testEndpoint", out var ep))
            {
                var path        = ep.TryGetProperty("path",        out var p)  ? p.GetString()  : null;
                var method      = ep.TryGetProperty("method",      out var m)  ? m.GetString()  : "GET";
                var body        = ep.TryGetProperty("body",        out var b)  ? b.GetString()  : null;
                var contentType = ep.TryGetProperty("contentType", out var ct) ? ct.GetString() : null;

                if (!string.IsNullOrEmpty(path))
                {
                    var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? path
                        : $"{baseUrl}{path}";
                    return (url, method?.ToUpperInvariant() ?? "GET", body, contentType);
                }
            }
        }
        catch (JsonException) { }

        return (baseUrl, "GET", null, null);
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

    private async Task<Dictionary<string, string>> StoreCredentialsAsync(
        TenantConnector connector,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken ct)
    {
        var secretNames = new Dictionary<string, string>();
        foreach (var (field, value) in credentials)
        {
            try
            {
                var secretName = $"connector-{connector.Id.Value}-{field}";
                var (storedName, _) = await keyVaultService.SetSecretAsync(secretName, value, ct: ct);
                secretNames[field] = storedName;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to store credential {Field} in Key Vault for connector {Id}",
                    field, connector.Id.Value);
            }
        }
        return secretNames;
    }
}
