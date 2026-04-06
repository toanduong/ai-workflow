using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlTenantConnectorRepository(WorkflowAIDbContext context) : ITenantConnectorRepository
{
    public async Task AddAsync(TenantConnector connector, CancellationToken cancellationToken = default)
    {
        context.TenantConnectors.Add(connector);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TenantConnector?> GetByIdAsync(TenantConnectorId id, CancellationToken cancellationToken = default)
        => await context.TenantConnectors.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TenantConnector>> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        => await context.TenantConnectors
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.ConnectorName)
            .ToListAsync(cancellationToken);

    public async Task<TenantConnector?> GetByTenantAndConnectorAsync(TenantId tenantId, string connectorName, CancellationToken cancellationToken = default)
        => await context.TenantConnectors
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ConnectorName == connectorName, cancellationToken);

    public async Task UpdateAsync(TenantConnector connector, CancellationToken cancellationToken = default)
    {
        context.TenantConnectors.Update(connector);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantConnectorId id, CancellationToken cancellationToken = default)
    {
        var connector = await context.TenantConnectors.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (connector is not null)
        {
            context.TenantConnectors.Remove(connector);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
