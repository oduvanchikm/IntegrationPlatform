using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection.Configurations;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Publication.DataAccess.DatabaseConnection;

public class PublicationDbContext(DbContextOptions<PublicationDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products { get; set; }
    public DbSet<DataInterface> DataInterfaces { get; set; }
    public DbSet<ApiInterface> ApiInterfaces { get; set; }
    public DbSet<DatabaseInterface> DatabaseInterfaces { get; set; }
    public DbSet<KafkaInterface> KafkaInterfaces { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("publication");

        modelBuilder.Entity<DataInterface>().ToTable("DataInterfaces");
        modelBuilder.Entity<ApiInterface>().ToTable("ApiInterface");
        modelBuilder.Entity<DatabaseInterface>().ToTable("DatabaseInterface");
        modelBuilder.Entity<KafkaInterface>().ToTable("KafkaInterface");
        modelBuilder.Entity<Product>().ToTable("Product");

        modelBuilder.ApplyConfiguration(new DataInterfaceConfiguration());

        modelBuilder.ApplyConfiguration(new DatabaseInterfaceConfiguration());
        modelBuilder.ApplyConfiguration(new KafkaInterfaceConfiguration());
        modelBuilder.ApplyConfiguration(new ApiInterfaceConfiguration());

        modelBuilder.ApplyConfiguration(new ProductConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}