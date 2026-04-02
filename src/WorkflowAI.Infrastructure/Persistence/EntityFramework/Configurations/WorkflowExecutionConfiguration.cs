using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Executions;
using WorkflowAI.Domain.Workflows;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class WorkflowExecutionConfiguration : IEntityTypeConfiguration<WorkflowExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowExecution> builder)
    {
        builder.ToTable("WorkflowExecutions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => ExecutionId.From(value));

        builder.Property(e => e.WorkflowId)
            .HasConversion(id => id.Value, value => WorkflowId.From(value))
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion(
                s => s.Name,
                name => ExecutionStatus.FromName(name)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.InputData)
            .HasColumnType("text");

        builder.Property(e => e.OutputData)
            .HasColumnType("text");

        builder.Property(e => e.TriggeredBy)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.CompletedAt);

        // Navigation relationship to StepExecutions
        builder.HasMany<StepExecution>()
            .WithOne()
            .HasForeignKey(s => s.WorkflowExecutionId)
            .IsRequired();
    }
}
