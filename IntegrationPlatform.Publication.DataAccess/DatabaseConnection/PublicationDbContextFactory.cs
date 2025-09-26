using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IntegrationPlatform.Publication.DataAccess.DatabaseConnection;

public class PublicationDbContextFactory : IDesignTimeDbContextFactory<PublicationDbContext>
{
    public PublicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PublicationDbContext>();
            
        optionsBuilder.UseNpgsql("Host=localhost;Port=5480;Database=integration_platform;Username=admin;Password=password;Search Path=publication");         
        
        return new PublicationDbContext(optionsBuilder.Options);
    }
}