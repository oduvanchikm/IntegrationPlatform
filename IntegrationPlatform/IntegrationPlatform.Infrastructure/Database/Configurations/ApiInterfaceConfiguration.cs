using IntegrationPlatform.IntegrationPlatform.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.IntegrationPlatform.Infrastructure.Database.Configurations;

public class ApiInterfaceConfiguration : IEntityTypeConfiguration<ApiInterface>
{
    public void Configure(EntityTypeBuilder<ApiInterface> builder)
    {
        builder.ToTable("ApiInterface");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Password)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Port)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(e => e.Host)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Endpoint)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Token)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => e.ProductInterfaceId)
            .IsUnique();
    }
}