using FluentAssertions;
using WorkflowAI.Domain.Approvals;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Domain.UnitTests.Approvals;

public class ApprovalRequestTests
{
    [Fact]
    public void Create_ShouldRaiseApprovalRequestedEvent()
    {
        var approval = ApprovalRequest.Create(
            Guid.NewGuid(), "Review Report", "Please review", null,
            "token123", DateTime.UtcNow.AddHours(1));

        approval.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowAI.Domain.Approvals.Events.ApprovalRequestedEvent>();
    }

    [Fact]
    public void Approve_ShouldChangeStatusAndRaiseEvent()
    {
        var approval = ApprovalRequest.Create(
            Guid.NewGuid(), "Test", null, null, "token", DateTime.UtcNow.AddHours(1));
        approval.ClearDomainEvents();

        var result = approval.Approve(UserId.New(), ApprovalChannel.Web, "Looks good");

        result.IsSuccess.Should().BeTrue();
        approval.Status.Should().Be(ApprovalStatus.Approved);
        approval.Actions.Should().HaveCount(1);
        approval.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowAI.Domain.Approvals.Events.ApprovalCompletedEvent>();
    }

    [Fact]
    public void Reject_ShouldChangeStatusAndRaiseEvent()
    {
        var approval = ApprovalRequest.Create(
            Guid.NewGuid(), "Test", null, null, "token", DateTime.UtcNow.AddHours(1));
        approval.ClearDomainEvents();

        var result = approval.Reject(UserId.New(), ApprovalChannel.Email, "Not acceptable");

        result.IsSuccess.Should().BeTrue();
        approval.Status.Should().Be(ApprovalStatus.Rejected);
    }

    [Fact]
    public void Approve_ShouldFail_WhenAlreadyApproved()
    {
        var approval = ApprovalRequest.Create(
            Guid.NewGuid(), "Test", null, null, "token", DateTime.UtcNow.AddHours(1));
        approval.Approve(UserId.New(), ApprovalChannel.Web);

        var result = approval.Approve(UserId.New(), ApprovalChannel.Slack);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Approval.NotPending");
    }

    [Fact]
    public void Escalate_ShouldChangeStatus()
    {
        var approval = ApprovalRequest.Create(
            Guid.NewGuid(), "Test", null, null, "token", DateTime.UtcNow.AddHours(1));
        approval.ClearDomainEvents();

        var result = approval.Escalate();

        result.IsSuccess.Should().BeTrue();
        approval.Status.Should().Be(ApprovalStatus.Escalated);
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenPastExpiresAt()
    {
        var approval = ApprovalRequest.Create(
            Guid.NewGuid(), "Test", null, null, "token", DateTime.UtcNow.AddSeconds(-1));

        approval.IsExpired().Should().BeTrue();
    }
}
