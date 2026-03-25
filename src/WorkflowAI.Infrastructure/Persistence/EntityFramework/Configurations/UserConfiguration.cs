using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => UserId.From(value));

        builder.Property(u => u.ExternalId).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.ExternalId).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();

        builder.Property(u => u.Role)
            .HasConversion(
                r => r.Name,
                name => UserRole.FromName(name)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.NotificationPreferences).HasColumnType("nvarchar(max)");
    }
}
