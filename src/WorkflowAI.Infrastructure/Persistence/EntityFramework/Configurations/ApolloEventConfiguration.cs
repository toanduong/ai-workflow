using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Apollo;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class ApolloEventConfiguration : IEntityTypeConfiguration<ApolloEvent>
{
    public void Configure(EntityTypeBuilder<ApolloEvent> builder)
    {
        builder.ToTable("ApolloEvents");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => ApolloEventId.From(value));

        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();

        builder.Property(e => e.Status)
            .HasConversion(s => s.Name, name => ApolloEventStatus.FromName(name)!)
            .HasMaxLength(50).IsRequired();

        builder.Property(e => e.Payload).HasColumnType("text").IsRequired();
        builder.Property(e => e.ClaudeResponse).HasColumnType("text");
        builder.Property(e => e.ErrorMessage).HasMaxLength(1000);

        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.CreatedAt);
    }
}
