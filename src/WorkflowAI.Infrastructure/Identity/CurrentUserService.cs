using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Infrastructure.Identity;

public sealed class CurrentUserService : ICurrentUserService
{
    public UserId? UserId { get; set; }
    public string? ExternalId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public bool IsAuthenticated => UserId is not null;
}
