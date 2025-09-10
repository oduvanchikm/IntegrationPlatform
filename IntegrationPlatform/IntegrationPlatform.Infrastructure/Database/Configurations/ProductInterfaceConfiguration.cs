using IntegrationPlatform.IntegrationPlatform.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntegrationPlatform.IntegrationPlatform.Infrastructure.Database.Configurations;

public class ProductInterfaceConfiguration : IEntityTypeConfiguration<ProductInterface>
{
    public void Configure(EntityTypeBuilder<ProductInterface> builder)
    {
        
    }
} 