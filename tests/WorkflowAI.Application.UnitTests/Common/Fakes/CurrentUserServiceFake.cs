using WorkflowAI.Application.Common.Interfaces;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Application.UnitTests.Common.Fakes;

internal sealed class CurrentUserServiceFake : ICurrentUserService
{
    public UserId? UserId { get; set; } = Domain.Users.UserId.New();
    public string? ExternalId { get; set; } = "ext-123";
    public string? DisplayName { get; set; } = "Test User";
    public string? Email { get; set; } = "test@example.com";
    public bool IsAuthenticated => UserId is not null;

    public static CurrentUserServiceFake Authenticated() => new();
    public static CurrentUserServiceFake Unauthenticated() => new() { UserId = null };
}
