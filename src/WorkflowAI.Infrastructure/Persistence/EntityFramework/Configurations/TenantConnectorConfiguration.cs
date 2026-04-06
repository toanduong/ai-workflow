using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class TenantConnectorConfiguration : IEntityTypeConfiguration<TenantConnector>
{
    public void Configure(EntityTypeBuilder<TenantConnector> builder)
    {
        builder.ToTable("TenantConnectors");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => TenantConnectorId.From(value));

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(e => e.ConnectorName).HasMaxLength(200).IsRequired();

        builder.Property(e => e.Metadata).HasColumnType("text").IsRequired();
        builder.Property(e => e.Info).HasColumnType("text").IsRequired();

        builder.Property(e => e.Status)
            .HasConversion(s => s.Name, name => TenantConnectorStatus.FromName(name)!)
            .HasMaxLength(50).IsRequired();

        builder.Property(e => e.FailureReason).HasMaxLength(1000);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.ConnectorName }).IsUnique();
    }
}
