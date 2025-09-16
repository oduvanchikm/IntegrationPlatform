using IntegrationPlatform.Publication.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.Publication.API.DatabaseConnection.Configurations;

public class DatabaseInterfaceConfiguration : IEntityTypeConfiguration<DatabaseInterface>
{
    public void Configure(EntityTypeBuilder<DatabaseInterface> builder)
    {
        builder.ToTable("DatabaseInterface", "publication");

        // builder.HasKey(e => e.Id);

        builder.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Scheme)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.DatabaseName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Password)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Host)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Port)
            .IsRequired()
            .HasMaxLength(10);
    }
}