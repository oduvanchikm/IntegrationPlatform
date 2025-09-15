using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.API.DatabaseConnection.Configurations;
using IntegrationPlatform.Publication.API.Models;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Publication.API.DatabaseConnection;

public class PublicationDbContext(DbContextOptions<PublicationDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products { get; set; }
    public DbSet<DataInterface> DataInterfaces { get; set; }
    public DbSet<ApiInterface> ApiInterfaces { get; set; }
    public DbSet<DatabaseInterface> DatabaseInterfaces { get; set; }
    public DbSet<KafkaInterface> KafkaInterfaces { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new DataInterfaceConfiguration());
        modelBuilder.ApplyConfiguration(new DatabaseInterfaceConfiguration());
        modelBuilder.ApplyConfiguration(new KafkaInterfaceConfiguration());
        modelBuilder.ApplyConfiguration(new ApiInterfaceConfiguration());
        
        base.OnModelCreating(modelBuilder);
    }
}