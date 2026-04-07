using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Application.TenantConnectors.Commands.ValidateTenantConnector;

public sealed class ValidateTenantConnectorCommandHandler(
    ITenantConnectorRepository repository,
    ICredentialApplicatorFactory credentialApplicatorFactory,
    IKeyVaultService keyVaultService,
    ICurrentUserService currentUserService,
    ILogger<ValidateTenantConnectorCommandHandler> logger,
    HttpClient httpClient)
    : IRequestHandler<ValidateTenantConnectorCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        ValidateTenantConnectorCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
            return Error.Unauthorized("Auth.Required", "User must be authenticated.");

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

            credentialApplicatorFactory.Resolve(authType).Apply(httpRequest, request.CredentialFields);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var secretNames = await StoreCredentialsAsync(
                    connector.Id, request.CredentialFields, cancellationToken);
                connector.Activate(secretNames);
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
            logger.LogError(ex, "Connector validation failed for {ConnectorId}", request.TenantConnectorId);
            connector.MarkFailed(ex.Message);
            await repository.UpdateAsync(connector, cancellationToken);
            return false;
        }
    }

    private async Task<Dictionary<string, string>> StoreCredentialsAsync(
        TenantConnectorId connectorId,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        var secretNames = new Dictionary<string, string>();
        foreach (var (fieldName, value) in fields)
        {
            var secretName = $"connector-{connectorId.Value}-{fieldName}";
            await keyVaultService.SetSecretAsync(secretName, value, ct: cancellationToken);
            secretNames[fieldName] = secretName;
        }
        return secretNames;
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
