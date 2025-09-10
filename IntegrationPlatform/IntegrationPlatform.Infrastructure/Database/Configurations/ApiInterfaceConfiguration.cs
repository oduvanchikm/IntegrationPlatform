using IntegrationPlatform.IntegrationPlatform.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.IntegrationPlatform.Infrastructure.Database.Configurations;

public class ApiInterfaceConfiguration : IEntityTypeConfiguration<ApiInterface>
{
    public void Configure(EntityTypeBuilder<ApiInterface> builder)
    {
        
    }
}