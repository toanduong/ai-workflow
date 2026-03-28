using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowAI.Domain.Channels;

namespace WorkflowAI.Infrastructure.Persistence.EntityFramework.Configurations;

public sealed class NotificationChannelConfiguration : IEntityTypeConfiguration<NotificationChannel>
{
    public void Configure(EntityTypeBuilder<NotificationChannel> builder)
    {
        builder.ToTable("NotificationChannels");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => ChannelId.From(value));

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();

        builder.Property(c => c.ChannelType)
            .HasConversion(
                ct => ct.Name,
                name => ChannelType.FromName(name)!)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ConnectionConfig).HasColumnType("text");
    }
}
