using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Notifications;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasConversion(id => id.Value, value => NotificationId.From(value));

        builder.Property(n => n.ApprovalRequestId);
        builder.Property(n => n.ChannelId).IsRequired();

        builder.Property(n => n.Type)
            .HasConversion(
                t => t.Name,
                name => NotificationType.FromName(name)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.Subject)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(n => n.Body)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(n => n.Status)
            .HasConversion(
                s => s.Name,
                name => NotificationStatus.FromName(name)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.RecipientAddress)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(n => n.RetryCount).IsRequired();
        builder.Property(n => n.SentAt);
        builder.Property(n => n.DeliveredAt);
    }
}
