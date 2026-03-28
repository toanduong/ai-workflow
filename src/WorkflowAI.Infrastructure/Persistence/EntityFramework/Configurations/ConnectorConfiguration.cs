using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Connectors;
using WorkflowAI.Domain.Users;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class ConnectorConfiguration : IEntityTypeConfiguration<Connector>
{
    public void Configure(EntityTypeBuilder<Connector> builder)
    {
        builder.ToTable("Connectors");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => ConnectorId.From(value));

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();

        builder.Property(c => c.ConnectorType)
            .HasConversion(t => t.Name, name => ConnectorType.FromName(name)!)
            .HasMaxLength(50).IsRequired();

        builder.Property(c => c.AuthModel)
            .HasConversion(a => a.Name, name => AuthModel.FromName(name)!)
            .HasMaxLength(50).IsRequired();

        builder.Property(c => c.Status)
            .HasConversion(s => s.Name, name => ConnectorStatus.FromName(name)!)
            .HasMaxLength(50).IsRequired();

        builder.Property(c => c.CreatedByUserId)
            .HasConversion(id => id.Value, value => UserId.From(value));

        builder.Property(c => c.AzureApiConnectionId).HasMaxLength(500);
        builder.Property(c => c.ManagedApiId).HasMaxLength(500);
        builder.Property(c => c.Configuration).HasColumnType("text");
    }
}
