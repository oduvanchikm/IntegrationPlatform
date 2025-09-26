using IntegrationPlatform.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.Subscription.DataAccess.DatabaseConnection.Configurations;

public class OrchestrationConfigConfiguration : IEntityTypeConfiguration<OrchestrationConfig>
{
    public void Configure(EntityTypeBuilder<OrchestrationConfig> builder)
    {
        builder.ToTable("OrchestrationConfig", "subscription");

        builder.HasKey(e => e.Id);

        builder.HasOne(oc => oc.DataInterface)
            .WithOne(di => di.OrchestrationConfig)
            .HasForeignKey<OrchestrationConfig>(oc => oc.InterfaceSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(oc => oc.InterfacePublicationId)
            .IsRequired();

        builder.Property(oc => oc.IntegrationPattern)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(oc => oc.ScheduleCron)
            .IsRequired()
            .HasDefaultValue("*/5 * * * *")
            .HasMaxLength(50);

        builder.Property(oc => oc.MaxRetryAttempts)
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(oc => oc.RetryDelaySeconds)
            .IsRequired()
            .HasDefaultValue(60);

        builder.Property(oc => oc.ExecutionTimeoutSeconds)
            .IsRequired()
            .HasDefaultValue(300);

        builder.Property(oc => oc.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(oc => oc.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAddOrUpdate();
    }
}