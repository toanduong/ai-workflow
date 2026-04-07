namespace WorkflowAI.Domain.TenantConnectors;

public interface ITenantConnectorRepository
{
    // Table 1: TenantConnectors
    Task AddAsync(TenantConnector connector, CancellationToken ct = default);
    Task<TenantConnector?> GetByIdAsync(TenantConnectorId id, CancellationToken ct = default);
    Task<TenantConnector?> GetByTenantAndNameAsync(TenantId tenantId, string connectorName, CancellationToken ct = default);
    Task<IReadOnlyList<TenantConnector>> GetByTenantAsync(TenantId tenantId, CancellationToken ct = default);
    Task UpdateAsync(TenantConnector connector, CancellationToken ct = default);
    Task DeleteAsync(TenantConnectorId id, CancellationToken ct = default);

    // Table 2: TenantConnectorApis
    Task AddApiAsync(TenantConnectorApi api, CancellationToken ct = default);
    Task AddApisAsync(IEnumerable<TenantConnectorApi> apis, CancellationToken ct = default);
    Task<IReadOnlyList<TenantConnectorApi>> GetApisByConnectorAsync(TenantConnectorId tenantConnectorId, CancellationToken ct = default);
    Task<TenantConnectorApi?> GetApiByNameAsync(TenantConnectorId tenantConnectorId, string apiName, CancellationToken ct = default);
    Task DeleteApisByConnectorAsync(TenantConnectorId tenantConnectorId, CancellationToken ct = default);
}
