using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Approvals;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("ApprovalRequests");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => ApprovalRequestId.From(value));

        builder.Property(a => a.StepExecutionId).IsRequired();

        builder.Property(a => a.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasColumnType("text");

        builder.Property(a => a.ContextData)
            .HasColumnType("text");

        builder.Property(a => a.Status)
            .HasConversion(
                s => s.Name,
                name => ApprovalStatus.FromName(name)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.ApprovalToken)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(a => a.ApprovalToken).IsUnique();

        builder.Property(a => a.ExpiresAt).IsRequired();

        // Navigation relationship to ApprovalActions
        builder.HasMany<ApprovalAction>()
            .WithOne()
            .HasForeignKey(act => act.ApprovalRequestId)
            .IsRequired();
    }
}
