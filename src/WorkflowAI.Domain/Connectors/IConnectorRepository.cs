namespace WorkflowAI.Domain.Connectors;

public interface IConnectorRepository
{
    Task<Connector?> GetByIdAsync(ConnectorId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Connector>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Connector>> GetByTypeAsync(ConnectorType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Connector>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Connector>> GetExpiringAsync(TimeSpan threshold, CancellationToken cancellationToken = default);
    Task AddAsync(Connector connector, CancellationToken cancellationToken = default);
    Task UpdateAsync(Connector connector, CancellationToken cancellationToken = default);
    Task DeleteAsync(ConnectorId id, CancellationToken cancellationToken = default);
    Task<ConnectorCredential?> GetCredentialAsync(Guid credentialId, CancellationToken cancellationToken = default);
    Task AddCredentialAsync(ConnectorCredential credential, CancellationToken cancellationToken = default);
    Task UpdateCredentialAsync(ConnectorCredential credential, CancellationToken cancellationToken = default);
    Task DeleteCredentialAsync(Guid credentialId, CancellationToken cancellationToken = default);
}
