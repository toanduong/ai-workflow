using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Application.Common.Interfaces;

public interface IConnectorService
{
    Task<string> GenerateOAuthConsentUrlAsync(ConnectorId connectorId, ConnectorType connectorType, CancellationToken ct = default);
    Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> ExchangeOAuthCodeAsync(string code, string state, CancellationToken ct = default);
    Task<bool> ValidateConnectorAsync(Connector connector, CancellationToken ct = default);
    Task<(string AccessToken, DateTime ExpiresAt)> RefreshOAuthTokenAsync(string refreshToken, ConnectorType connectorType, CancellationToken ct = default);
}
