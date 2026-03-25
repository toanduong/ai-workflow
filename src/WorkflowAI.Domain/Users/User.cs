using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Users;

public sealed class User : Entity<UserId>
{
    public string ExternalId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public UserRole Role { get; private set; } = UserRole.Viewer;
    public string? NotificationPreferences { get; private set; }

    private User() { }

    public static User Create(string externalId, string displayName, string email, UserRole role)
    {
        return new User
        {
            Id = UserId.New(),
            ExternalId = externalId,
            DisplayName = displayName,
            Email = email,
            Role = role
        };
    }

    public void UpdateProfile(string displayName, string email)
    {
        DisplayName = displayName;
        Email = email;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateNotificationPreferences(string? preferences)
    {
        NotificationPreferences = preferences;
        UpdatedAt = DateTime.UtcNow;
    }
}
