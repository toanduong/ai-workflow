using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Users;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("Workflows");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => WorkflowId.From(value));

        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(1000);
        builder.Property(w => w.LogicAppResourceId).HasMaxLength(500);

        builder.Property(w => w.Status)
            .HasConversion(s => s.Name, name => WorkflowStatus.FromName(name)!)
            .HasMaxLength(50).IsRequired();

        builder.Property(w => w.CreatedByUserId)
            .HasConversion(id => id.Value, value => UserId.From(value));

        builder.OwnsMany(w => w.Steps, step =>
        {
            step.ToTable("WorkflowSteps");
            step.HasKey(s => s.Id);

            step.Property(s => s.WorkflowId)
                .HasConversion(id => id.Value, value => WorkflowId.From(value));

            step.Property(s => s.Name).HasMaxLength(200).IsRequired();
            step.Property(s => s.Configuration).HasColumnType("text");
            step.Property(s => s.RequiredRole).HasMaxLength(100);

            step.Property(s => s.StepType)
                .HasConversion(t => t.Name, name => StepType.FromName(name)!)
                .HasMaxLength(50).IsRequired();

            step.Property(s => s.OnTimeoutAction)
                .HasConversion(t => t.Name, name => TimeoutAction.FromName(name)!)
                .HasMaxLength(50).IsRequired();

            step.Property(s => s.ConnectorId)
                .HasConversion(
                    id => id.HasValue ? id.Value.Value : (Guid?)null,
                    value => value.HasValue ? ConnectorId.From(value.Value) : null);

            step.WithOwner().HasForeignKey("WorkflowId");
        });
    }
}
