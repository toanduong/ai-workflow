using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class TenantConnectorApiConfiguration : IEntityTypeConfiguration<TenantConnectorApi>
{
    public void Configure(EntityTypeBuilder<TenantConnectorApi> builder)
    {
        builder.ToTable("TenantConnectorApis");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => TenantConnectorApiId.From(v));

        builder.Property(e => e.TenantConnectorId)
            .HasConversion(id => id.Value, v => TenantConnectorId.From(v))
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, v => TenantId.From(v))
            .IsRequired();

        builder.Property(e => e.ConnectorType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ApiName)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(e => e.HttpMethod)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.UrlTemplate)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.Metadata)
            .HasColumnType("text")
            .IsRequired();

        // Index for fast lookup by connector
        builder.HasIndex(e => e.TenantConnectorId);

        // One API name per connector
        builder.HasIndex(nameof(TenantConnectorApi.TenantConnectorId), nameof(TenantConnectorApi.ApiName))
            .IsUnique();
    }
}
