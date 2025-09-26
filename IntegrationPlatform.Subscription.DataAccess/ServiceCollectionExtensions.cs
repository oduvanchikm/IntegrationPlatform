using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationPlatform.Subscription.DataAccess;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSubscriptionDbContext(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<SubscriptionDbContext>(options =>
            options.UseNpgsql(connectionString));
            
        return services;
    }
}