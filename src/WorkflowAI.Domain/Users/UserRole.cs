using WorkflowAI.Domain.Common;

namespace WorkflowAI.Domain.Users;

public sealed class UserRole : Enumeration<UserRole>
{
    public static readonly UserRole Admin = new(1, nameof(Admin));
    public static readonly UserRole Approver = new(2, nameof(Approver));
    public static readonly UserRole Viewer = new(3, nameof(Viewer));

    private UserRole(int id, string name) : base(id, name) { }
}
