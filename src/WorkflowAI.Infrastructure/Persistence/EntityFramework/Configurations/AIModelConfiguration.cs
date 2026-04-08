using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.AIAgent;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class AIModelConfiguration : IEntityTypeConfiguration<AIModel>
{
    public void Configure(EntityTypeBuilder<AIModel> builder)
    {
        builder.ToTable("AIModels");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => AIModelId.From(value));

        builder.Property(m => m.ModelId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(m => m.ModelId)
            .IsUnique();

        builder.Property(m => m.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.InputCostPer1KTokens)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(m => m.OutputCostPer1KTokens)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(m => m.ContextWindow).IsRequired();
        builder.Property(m => m.IsEnabled).IsRequired();
        builder.Property(m => m.IsDefault).IsRequired();

        builder.Property(m => m.Notes)
            .HasMaxLength(500);
    }
}
