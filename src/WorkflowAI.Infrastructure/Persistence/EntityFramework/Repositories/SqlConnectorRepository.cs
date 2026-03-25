using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.Connectors;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlConnectorRepository(WorkflowAIDbContext context) : IConnectorRepository
{
    public async Task<Connector?> GetByIdAsync(ConnectorId id, CancellationToken ct = default)
        => await context.Connectors.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Connector>> GetAllAsync(CancellationToken ct = default)
        => await context.Connectors.ToListAsync(ct);

    public async Task<IReadOnlyList<Connector>> GetByTypeAsync(ConnectorType type, CancellationToken ct = default)
        => await context.Connectors.Where(c => c.ConnectorType == type).ToListAsync(ct);

    public async Task<IReadOnlyList<Connector>> GetActiveAsync(CancellationToken ct = default)
        => await context.Connectors.Where(c => c.Status == ConnectorStatus.Active).ToListAsync(ct);

    public async Task<IReadOnlyList<Connector>> GetExpiringAsync(TimeSpan threshold, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.Add(threshold);
        return await context.Connectors
            .Where(c => c.Status == ConnectorStatus.Active && c.ExpiresAt != null && c.ExpiresAt <= cutoff)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Connector connector, CancellationToken ct = default)
    {
        context.Connectors.Add(connector);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Connector connector, CancellationToken ct = default)
    {
        context.Connectors.Update(connector);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ConnectorId id, CancellationToken ct = default)
    {
        var connector = await GetByIdAsync(id, ct);
        if (connector is not null)
        {
            context.Connectors.Remove(connector);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<ConnectorCredential?> GetCredentialAsync(Guid credentialId, CancellationToken ct = default)
        => await context.ConnectorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId, ct);

    public async Task AddCredentialAsync(ConnectorCredential credential, CancellationToken ct = default)
    {
        context.ConnectorCredentials.Add(credential);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateCredentialAsync(ConnectorCredential credential, CancellationToken ct = default)
    {
        context.ConnectorCredentials.Update(credential);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteCredentialAsync(Guid credentialId, CancellationToken ct = default)
    {
        var credential = await GetCredentialAsync(credentialId, ct);
        if (credential is not null)
        {
            context.ConnectorCredentials.Remove(credential);
            await context.SaveChangesAsync(ct);
        }
    }
}
