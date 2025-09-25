using IntegrationPlatform.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.Subscription.DataAccess.DatabaseConnection.Configurations;

public class ApiInterfaceConfiguration : IEntityTypeConfiguration<ApiInterface>
{
    public void Configure(EntityTypeBuilder<ApiInterface> builder)
    {
        builder.ToTable("ApiInterface", "subscription");

        builder.Property(e => e.Host)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Port)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(e => e.Endpoint)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Username)
            .HasMaxLength(100);

        builder.Property(e => e.Password)
            .HasMaxLength(100);

        builder.Property(e => e.Token)
            .HasMaxLength(1000);
    }
}