using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Executions;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class StepExecutionConfiguration : IEntityTypeConfiguration<StepExecution>
{
    public void Configure(EntityTypeBuilder<StepExecution> builder)
    {
        builder.ToTable("StepExecutions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.WorkflowExecutionId)
            .HasConversion(id => id.Value, value => ExecutionId.From(value))
            .IsRequired();

        builder.Property(s => s.WorkflowStepId).IsRequired();

        builder.Property(s => s.Status)
            .HasConversion(s => s.Name, name => StepExecutionStatus.FromName(name)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.InputData).HasColumnType("text");
        builder.Property(s => s.OutputData).HasColumnType("text");

        builder.Property(s => s.StartedAt);
        builder.Property(s => s.CompletedAt);

        builder.Property(s => s.ErrorMessage).HasColumnType("text");

        builder.HasIndex(s => s.WorkflowExecutionId);
    }
}
