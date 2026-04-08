namespace WorkflowAI.Domain.TenantConnectors;

public interface ITenantConnectorApiHealthCheckRepository
{
    Task AddAsync(TenantConnectorApiHealthCheck check, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<TenantConnectorApiHealthCheck> checks, CancellationToken ct = default);

    /// <summary>Returns paginated history for a connector, newest first. Optionally filtered by ApiName.</summary>
    Task<IReadOnlyList<TenantConnectorApiHealthCheck>> GetByConnectorAsync(
        TenantConnectorId tenantConnectorId,
        string? apiName = null,
        int pageSize = 50,
        int pageIndex = 0,
        CancellationToken ct = default);

    /// <summary>Returns the most recent health check per API — one row per ApiName.</summary>
    Task<IReadOnlyList<TenantConnectorApiHealthCheck>> GetLatestPerApiAsync(
        TenantConnectorId tenantConnectorId,
        CancellationToken ct = default);
}
