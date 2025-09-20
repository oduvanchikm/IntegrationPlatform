using IntegrationPlatform.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.Subscription.API.DatabaseConnection.Configurations;

public class KafkaInterfaceConfiguration : IEntityTypeConfiguration<KafkaInterface>
{
    public void Configure(EntityTypeBuilder<KafkaInterface> builder)
    {
        builder.ToTable("KafkaInterface", "subscription");

        builder.Property(k => k.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(k => k.Password)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(k => k.BootstrapServers)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(k => k.TopicName)
            .IsRequired()
            .HasMaxLength(100);
    }
}
