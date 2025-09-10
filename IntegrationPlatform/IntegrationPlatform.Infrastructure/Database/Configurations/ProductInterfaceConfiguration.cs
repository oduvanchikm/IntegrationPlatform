using IntegrationPlatform.IntegrationPlatform.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.IntegrationPlatform.Infrastructure.Database.Configurations;

public class ProductInterfaceConfiguration : IEntityTypeConfiguration<ProductInterface>
{
    public void Configure(EntityTypeBuilder<ProductInterface> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.InterfaceType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(p => p.Specification)
            .HasColumnType("jsonb");

        builder.HasOne(p => p.Product)
            .WithMany(i => i.Interfaces)
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ApiInterface)
            .WithOne(p => p.ProductInterface)
            .HasForeignKey<ApiInterface>(p => p.ProductInterfaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.DatabaseInterface)
            .WithOne(p => p.ProductInterface)
            .HasForeignKey<ApiInterface>(p => p.ProductInterfaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.KafkaInterface)
            .WithOne(p => p.ProductInterface)
            .HasForeignKey<ApiInterface>(p => p.ProductInterfaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}