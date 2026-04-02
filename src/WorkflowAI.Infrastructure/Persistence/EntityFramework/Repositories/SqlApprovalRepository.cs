using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.Approvals;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Repositories;

public sealed class SqlApprovalRepository(WorkflowAIDbContext context) : IApprovalRepository
{
    public async Task<ApprovalRequest?> GetByIdAsync(ApprovalRequestId id, CancellationToken cancellationToken = default)
        => await context.Approvals.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<ApprovalRequest?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        => await context.Approvals.FirstOrDefaultAsync(a => a.ApprovalToken == token, cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync(CancellationToken cancellationToken = default)
        => await context.Approvals
            .Where(a => a.Status == ApprovalStatus.Pending)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequest>> GetExpiredPendingAsync(CancellationToken cancellationToken = default)
        => await context.Approvals
            .Where(a => a.Status == ApprovalStatus.Pending && a.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        context.Approvals.Add(request);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        context.Approvals.Update(request);
        await context.SaveChangesAsync(cancellationToken);
    }
}
