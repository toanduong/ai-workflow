using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Connectors;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class ConnectorCredentialConfiguration : IEntityTypeConfiguration<ConnectorCredential>
{
    public void Configure(EntityTypeBuilder<ConnectorCredential> builder)
    {
        builder.ToTable("ConnectorCredentials");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ConnectorId)
            .HasConversion(id => id.Value, value => ConnectorId.From(value));

        builder.Property(c => c.KeyVaultSecretName).HasMaxLength(256).IsRequired();
        builder.Property(c => c.KeyVaultSecretVersion).HasMaxLength(256);

        builder.Property(c => c.CredentialType)
            .HasConversion(t => t.Name, name => CredentialType.FromName(name)!)
            .HasMaxLength(50).IsRequired();
    }
}
