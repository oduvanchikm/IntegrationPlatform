using IntegrationPlatform.IntegrationPlatform.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.IntegrationPlatform.Infrastructure.Database.Configurations;

public class KafkaInterfaceConfiguration : IEntityTypeConfiguration<KafkaInterface>
{
    public void Configure(EntityTypeBuilder<KafkaInterface> builder)
    {
        builder.ToTable("KafkaInterface");

        builder.HasKey(c => c.Id);

        builder.Property(k => k.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(k => k.Password)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(k => k.BootstrapServer)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(k => k.TopicName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => e.ProductInterfaceId)
            .IsUnique();
    }
}