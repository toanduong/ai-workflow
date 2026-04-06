namespace WorkflowAI.Domain.TenantConnectors;

public interface ITenantConnectorRepository
{
    Task AddAsync(TenantConnector connector, CancellationToken cancellationToken = default);
    Task<TenantConnector?> GetByIdAsync(TenantConnectorId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantConnector>> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<TenantConnector?> GetByTenantAndConnectorAsync(TenantId tenantId, string connectorName, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantConnector connector, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantConnectorId id, CancellationToken cancellationToken = default);
}
