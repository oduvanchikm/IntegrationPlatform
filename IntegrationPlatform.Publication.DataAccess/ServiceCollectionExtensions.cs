using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationPlatform.Publication.DataAccess;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPublicationDbContext(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<PublicationDbContext>(options =>
            options.UseNpgsql(connectionString));
            
        return services;
    }
}