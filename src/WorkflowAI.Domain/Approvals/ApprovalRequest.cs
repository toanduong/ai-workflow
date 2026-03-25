using WorkflowAI.Domain.Approvals.Events;
using WorkflowAI.Domain.Common;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Domain.Approvals;

public sealed class ApprovalRequest : AggregateRoot<ApprovalRequestId>
{
    private readonly List<ApprovalAction> _actions = [];

    public Guid StepExecutionId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ContextData { get; private set; }
    public ApprovalStatus Status { get; private set; } = ApprovalStatus.Pending;
    public string ApprovalToken { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public IReadOnlyList<ApprovalAction> Actions => _actions.AsReadOnly();

    private ApprovalRequest() { }

    public static ApprovalRequest Create(
        Guid stepExecutionId,
        string title,
        string? description,
        string? contextData,
        string approvalToken,
        DateTime expiresAt)
    {
        var request = new ApprovalRequest
        {
            Id = ApprovalRequestId.New(),
            StepExecutionId = stepExecutionId,
            Title = title,
            Description = description,
            ContextData = contextData,
            ApprovalToken = approvalToken,
            ExpiresAt = expiresAt,
            Status = ApprovalStatus.Pending
        };

        request.RaiseDomainEvent(new ApprovalRequestedEvent(request.Id, stepExecutionId));
        return request;
    }

    public Result Approve(UserId userId, ApprovalChannel channel, string? comment = null)
    {
        if (Status != ApprovalStatus.Pending && Status != ApprovalStatus.Escalated)
            return Error.Validation("Approval.NotPending", "Approval is not in a pending state.");

        var action = ApprovalAction.Create(Id, userId, "Approve", channel, comment);
        _actions.Add(action);
        Status = ApprovalStatus.Approved;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ApprovalCompletedEvent(Id, StepExecutionId, "Approve"));
        return Result.Success();
    }

    public Result Reject(UserId userId, ApprovalChannel channel, string? comment = null)
    {
        if (Status != ApprovalStatus.Pending && Status != ApprovalStatus.Escalated)
            return Error.Validation("Approval.NotPending", "Approval is not in a pending state.");

        var action = ApprovalAction.Create(Id, userId, "Reject", channel, comment);
        _actions.Add(action);
        Status = ApprovalStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ApprovalCompletedEvent(Id, StepExecutionId, "Reject"));
        return Result.Success();
    }

    public Result Escalate()
    {
        if (Status != ApprovalStatus.Pending)
            return Error.Validation("Approval.NotPending", "Only pending approvals can be escalated.");

        Status = ApprovalStatus.Escalated;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ApprovalEscalatedEvent(Id, StepExecutionId));
        return Result.Success();
    }

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}
