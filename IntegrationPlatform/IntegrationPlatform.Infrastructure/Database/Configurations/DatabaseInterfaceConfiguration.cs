using IntegrationPlatform.IntegrationPlatform.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.IntegrationPlatform.Infrastructure.Database.Configurations;

public class DatabaseInterfaceConfiguration : IEntityTypeConfiguration<DatabaseInterface>
{
    public void Configure(EntityTypeBuilder<DatabaseInterface> builder)
    {
        
    }
}