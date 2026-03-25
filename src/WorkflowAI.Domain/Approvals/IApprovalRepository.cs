namespace WorkflowAI.Domain.Approvals;

public interface IApprovalRepository
{
    Task<ApprovalRequest?> GetByIdAsync(ApprovalRequestId id, CancellationToken cancellationToken = default);
    Task<ApprovalRequest?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApprovalRequest>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApprovalRequest>> GetExpiredPendingAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
}
