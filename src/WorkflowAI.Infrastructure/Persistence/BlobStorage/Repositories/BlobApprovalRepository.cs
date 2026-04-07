using WorkflowAI.Domain.Approvals;

namespace WorkflowAI.Infrastructure.Persistence.BlobStorage.Repositories;

internal sealed class BlobApprovalRepository(BlobStorageContext context)
    : BlobRepositoryBase(context.Approvals), IApprovalRepository
{
    public Task<ApprovalRequest?> GetByIdAsync(ApprovalRequestId id, CancellationToken ct = default)
        => GetAsync<ApprovalRequest>(id.Value, ct);

    public async Task<ApprovalRequest?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        var all = await GetAllAsync<ApprovalRequest>(ct);
        return all.FirstOrDefault(a => a.ApprovalToken == token);
    }

    public async Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync(CancellationToken ct = default)
    {
        var all = await GetAllAsync<ApprovalRequest>(ct);
        return all.Where(a => a.Status == ApprovalStatus.Pending).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<ApprovalRequest>> GetExpiredPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var all = await GetAllAsync<ApprovalRequest>(ct);
        return all.Where(a => a.Status == ApprovalStatus.Pending && a.ExpiresAt < now).ToList().AsReadOnly();
    }

    public Task AddAsync(ApprovalRequest request, CancellationToken ct = default)
        => SaveAsync(request.Id.Value, request, overwrite: false, ct);

    public Task UpdateAsync(ApprovalRequest request, CancellationToken ct = default)
        => SaveAsync(request.Id.Value, request, overwrite: true, ct);
}
