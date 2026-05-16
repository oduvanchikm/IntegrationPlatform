using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection.Configurations;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;

public class SubscriptionDbContext(DbContextOptions<SubscriptionDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products { get; set; }
    public DbSet<DataInterface> DataInterfaces { get; set; }
    public DbSet<ApiInterface> ApiInterfaces { get; set; }
    public DbSet<DatabaseInterface> DatabaseInterfaces { get; set; }
    public DbSet<KafkaInterface> KafkaInterfaces { get; set; }
    public DbSet<OrchestrationConfig> OrchestrationConfigs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("subscription");

        modelBuilder.Entity<DataInterface>().ToTable("DataInterfaces");
        modelBuilder.Entity<ApiInterface>().ToTable("ApiInterface");
        modelBuilder.Entity<DatabaseInterface>().ToTable("DatabaseInterface");
        modelBuilder.Entity<KafkaInterface>().ToTable("KafkaInterface");
        modelBuilder.Entity<OrchestrationConfig>().ToTable("OrchestrationConfig");
        modelBuilder.Entity<Product>().ToTable("Product");

        modelBuilder.ApplyConfiguration(new DataInterfaceConfiguration());

        modelBuilder.ApplyConfiguration(new DatabaseInterfaceConfiguration());
        modelBuilder.ApplyConfiguration(new KafkaInterfaceConfiguration());
        modelBuilder.ApplyConfiguration(new ApiInterfaceConfiguration());

        modelBuilder.ApplyConfiguration(new ProductConfiguration());

        modelBuilder.ApplyConfiguration(new OrchestrationConfigConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}