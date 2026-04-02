using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Approvals;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class ApprovalActionConfiguration : IEntityTypeConfiguration<ApprovalAction>
{
    public void Configure(EntityTypeBuilder<ApprovalAction> builder)
    {
        builder.ToTable("ApprovalActions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ApprovalRequestId)
            .HasConversion(id => id.Value, value => ApprovalRequestId.From(value))
            .IsRequired();

        builder.Property(a => a.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .IsRequired();

        builder.Property(a => a.Action)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Channel)
            .HasConversion(
                c => c.Name,
                name => ApprovalChannel.FromName(name)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Comment)
            .HasColumnType("text");

        builder.Property(a => a.ActedAt).IsRequired();

        builder.HasIndex(a => a.ApprovalRequestId);
    }
}
