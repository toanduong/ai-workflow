namespace WorkflowAI.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateApprovalToken(Guid approvalRequestId, string action, DateTime expiresAt);
    ApprovalTokenPayload? ValidateApprovalToken(string token);
}

public sealed record ApprovalTokenPayload(Guid ApprovalRequestId, string Action, DateTime ExpiresAt);
