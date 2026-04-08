using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlTenantConnectorApiHealthCheckRepository(WorkflowAIDbContext context)
    : ITenantConnectorApiHealthCheckRepository
{
    public async Task AddAsync(TenantConnectorApiHealthCheck check, CancellationToken ct = default)
    {
        context.TenantConnectorApiHealthChecks.Add(check);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<TenantConnectorApiHealthCheck> checks, CancellationToken ct = default)
    {
        context.TenantConnectorApiHealthChecks.AddRange(checks);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TenantConnectorApiHealthCheck>> GetByConnectorAsync(
        TenantConnectorId tenantConnectorId,
        string? apiName = null,
        int pageSize = 50,
        int pageIndex = 0,
        CancellationToken ct = default)
    {
        var query = context.TenantConnectorApiHealthChecks
            .Where(c => c.TenantConnectorId == tenantConnectorId);

        if (!string.IsNullOrEmpty(apiName))
            query = query.Where(c => c.ApiName == apiName);

        return await query
            .OrderByDescending(c => c.CheckedAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TenantConnectorApiHealthCheck>> GetLatestPerApiAsync(
        TenantConnectorId tenantConnectorId,
        CancellationToken ct = default)
    {
        return await context.TenantConnectorApiHealthChecks
            .Where(c => c.TenantConnectorId == tenantConnectorId)
            .GroupBy(c => c.ApiName)
            .Select(g => g.OrderByDescending(c => c.CheckedAt).First())
            .ToListAsync(ct);
    }
}
