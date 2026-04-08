using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class TenantConnectorApiHealthCheckConfiguration
    : IEntityTypeConfiguration<TenantConnectorApiHealthCheck>
{
    public void Configure(EntityTypeBuilder<TenantConnectorApiHealthCheck> builder)
    {
        builder.ToTable("TenantConnectorApiHealthChecks");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => TenantConnectorApiHealthCheckId.From(v));

        builder.Property(e => e.TenantConnectorApiId)
            .HasConversion(id => id.Value, v => TenantConnectorApiId.From(v))
            .IsRequired();

        builder.Property(e => e.TenantConnectorId)
            .HasConversion(id => id.Value, v => TenantConnectorId.From(v))
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, v => TenantId.From(v))
            .IsRequired();

        builder.Property(e => e.ApiName)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(e => e.HttpMethod)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.ResolvedUrl)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.CheckedAt)
            .IsRequired();

        builder.Property(e => e.IsSuccess)
            .IsRequired();

        builder.Property(e => e.StatusCode)
            .IsRequired(false);

        builder.Property(e => e.DurationMs)
            .IsRequired();

        builder.Property(e => e.FailureReason)
            .HasMaxLength(1000)
            .IsRequired(false);

        // Fast lookup by connector (most common query)
        builder.HasIndex(e => e.TenantConnectorId);

        // Lookup by specific API
        builder.HasIndex(e => e.TenantConnectorApiId);

        // Dashboard: latest checks for a connector ordered by time
        builder.HasIndex(e => new { e.TenantConnectorId, e.CheckedAt });
    }
}
