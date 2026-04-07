using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlTenantConnectorRepository(WorkflowAIDbContext context)
    : ITenantConnectorRepository
{
    // ── Table 1: TenantConnectors ──────────────────────────────────────────────

    public async Task AddAsync(TenantConnector connector, CancellationToken ct = default)
    {
        context.TenantConnectors.Add(connector);
        await context.SaveChangesAsync(ct);
    }

    public async Task<TenantConnector?> GetByIdAsync(TenantConnectorId id, CancellationToken ct = default)
        => await context.TenantConnectors
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<TenantConnector?> GetByTenantAndNameAsync(
        TenantId tenantId, string connectorName, CancellationToken ct = default)
        => await context.TenantConnectors
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.ConnectorName == connectorName, ct);

    public async Task<IReadOnlyList<TenantConnector>> GetByTenantAsync(
        TenantId tenantId, CancellationToken ct = default)
        => await context.TenantConnectors
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.ConnectorName)
            .ToListAsync(ct);

    public async Task UpdateAsync(TenantConnector connector, CancellationToken ct = default)
    {
        context.TenantConnectors.Update(connector);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(TenantConnectorId id, CancellationToken ct = default)
    {
        var connector = await GetByIdAsync(id, ct);
        if (connector is not null)
        {
            context.TenantConnectors.Remove(connector);
            await context.SaveChangesAsync(ct);
        }
    }

    // ── Table 2: TenantConnectorApis ──────────────────────────────────────────

    public async Task AddApiAsync(TenantConnectorApi api, CancellationToken ct = default)
    {
        context.TenantConnectorApis.Add(api);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddApisAsync(IEnumerable<TenantConnectorApi> apis, CancellationToken ct = default)
    {
        context.TenantConnectorApis.AddRange(apis);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TenantConnectorApi>> GetApisByConnectorAsync(
        TenantConnectorId tenantConnectorId, CancellationToken ct = default)
        => await context.TenantConnectorApis
            .Where(a => a.TenantConnectorId == tenantConnectorId)
            .OrderBy(a => a.ApiName)
            .ToListAsync(ct);

    public async Task<TenantConnectorApi?> GetApiByNameAsync(
        TenantConnectorId tenantConnectorId, string apiName, CancellationToken ct = default)
        => await context.TenantConnectorApis
            .FirstOrDefaultAsync(a =>
                a.TenantConnectorId == tenantConnectorId &&
                a.ApiName == apiName, ct);

    public async Task DeleteApisByConnectorAsync(
        TenantConnectorId tenantConnectorId, CancellationToken ct = default)
    {
        var apis = await context.TenantConnectorApis
            .Where(a => a.TenantConnectorId == tenantConnectorId)
            .ToListAsync(ct);

        context.TenantConnectorApis.RemoveRange(apis);
        await context.SaveChangesAsync(ct);
    }
}
