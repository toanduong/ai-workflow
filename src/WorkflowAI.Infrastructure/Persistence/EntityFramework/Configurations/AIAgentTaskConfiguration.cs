using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.AIAgent;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class AIAgentTaskConfiguration : IEntityTypeConfiguration<AIAgentTask>
{
    public void Configure(EntityTypeBuilder<AIAgentTask> builder)
    {
        builder.ToTable("AIAgentTasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => AIAgentTaskId.From(value));

        builder.Property(t => t.StepExecutionId).IsRequired();

        builder.Property(t => t.PromptTemplate)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(t => t.PromptVariables)
            .HasColumnType("text");

        builder.Property(t => t.Model)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion(
                s => s.Name,
                name => AITaskStatus.FromName(name)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.LLMResponse)
            .HasColumnType("text");

        builder.Property(t => t.TokensUsed).IsRequired();
        builder.Property(t => t.CompletedAt);
    }
}
