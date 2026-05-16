using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;

public class SubscriptionDbContextFactory : IDesignTimeDbContextFactory<SubscriptionDbContext>
{
    public SubscriptionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SubscriptionDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5480;Database=integration_platform;Username=admin;Password=password;Search Path=subscription");

        return new SubscriptionDbContext(optionsBuilder.Options);
    }
}