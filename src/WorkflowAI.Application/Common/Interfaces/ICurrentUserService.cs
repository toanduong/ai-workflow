using WorkflowAI.Domain.Users;

namespace WorkflowAI.Application.Common.Interfaces;

public interface ICurrentUserService
{
    UserId? UserId { get; }
    string? ExternalId { get; }
    string? DisplayName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}
