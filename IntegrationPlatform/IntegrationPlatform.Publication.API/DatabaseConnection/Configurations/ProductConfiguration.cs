using IntegrationPlatform.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.Publication.API.DatabaseConnection.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "publication");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.NameProduct)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.ProductType)
            .IsRequired()
            .HasConversion<string>();

        builder.HasMany(p => p.Interfaces)
            .WithOne(di => di.Product)
            .HasForeignKey(di => di.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}