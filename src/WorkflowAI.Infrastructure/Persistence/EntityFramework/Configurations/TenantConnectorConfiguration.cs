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
            .HasConversion(id => id.Value, v => TenantConnectorId.From(v));

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, v => TenantId.From(v))
            .IsRequired();

        builder.Property(e => e.ConnectorName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Metadata)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.Info)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion(s => s.Name, n => TenantConnectorStatus.FromName(n)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.FailureReason)
            .HasMaxLength(1000);

        builder.Property(e => e.CredentialSecretNames)
            .HasColumnType("text");

        // One connector per tenant per name
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(nameof(TenantConnector.TenantId), nameof(TenantConnector.ConnectorName))
            .IsUnique();
    }
}
