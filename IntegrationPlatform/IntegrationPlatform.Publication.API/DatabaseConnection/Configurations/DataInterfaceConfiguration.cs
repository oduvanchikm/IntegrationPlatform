using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.Publication.API.DatabaseConnection.Configurations;

public class DataInterfaceConfiguration : IEntityTypeConfiguration<DataInterface>
{
    public void Configure(EntityTypeBuilder<DataInterface> builder)
    {
        builder.ToTable("DataInterfaces", "publication");

        builder.HasKey(p => p.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.InterfaceType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasDefaultValue(ConnectionStatus.Draft)
            .HasConversion<string>();

        builder.HasOne(di => di.Product)
            .WithMany(p => p.Interfaces)
            .HasForeignKey(di => di.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}