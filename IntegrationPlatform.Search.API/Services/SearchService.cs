using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Search.API.DTO;
using IntegrationPlatform.Search.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Search.API.Services;

public class SearchService(IDbContextFactory<PublicationDbContext> publicationContext) : ISearchService
{
    public async Task<List<InterfaceSearchResult>> SearchInterfacesByNameProductAsync(string? productName)
    {
        await using var context = await publicationContext.CreateDbContextAsync();

        var products = context.DataInterfaces
            .Include(p => p.Product)
            .Where(di => di.Status == ConnectionStatus.Active)
            .AsQueryable();

        if (!string.IsNullOrEmpty(productName))
        {
            products = products.Where(di => di.Product.NameProduct.Contains(productName));
        }

        var result = await products
            .OrderBy(di => di.Product.NameProduct)
            .ThenBy(di => di.Name)
            .Select(di => new InterfaceSearchResult
            {
                Id = di.Id,
                Name = di.Name,
                Description = di.Description,
                InterfaceType = di.InterfaceType,
                ConnectionStatus = di.Status,
                ProductName = di.Product.NameProduct,
                ProductId = di.ProductId
            })
            .ToListAsync();

        return result;
    }

    public async Task<List<ProductSearchResult>> SearchProductsByInterfacesAsync(string? interfaceName)
    {
        await using var context = await publicationContext.CreateDbContextAsync();

        var interfaces = context.Products
            .Include(i => i.Interfaces)
            .Where(i => i.Interfaces.Any(i => i.Status == ConnectionStatus.Active))
            .AsQueryable();

        if (!string.IsNullOrEmpty(interfaceName))
        {
            interfaces = interfaces.Where(i => i.Interfaces.Any(i =>
                i.Name.Contains(interfaceName) && i.Status == ConnectionStatus.Active));
        }

        var result = await interfaces
            .OrderBy(i => i.NameProduct)
            .Select(i => new ProductSearchResult
            {
                Id = i.Id,
                Name = i.NameProduct,
                ProductType = i.ProductType,
                Interfaces = i.Interfaces
                    .Where(i => i.Status == ConnectionStatus.Active)
                    .OrderBy(i => i.Name)
                    .Select(i => new InterfaceInfo
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Description = i.Description,
                        InterfaceType = i.InterfaceType,
                        ConnectionStatus = i.Status
                    }).ToList()
            })
            .ToListAsync();

        return result;
    }

    public async Task<List<InterfaceSearchResult>> SearchInterfacesByTypeAsync(InterfaceType? interfaceType)
    {
        await using var context = await publicationContext.CreateDbContextAsync();

        var products = context.DataInterfaces
            .Include(p => p.Product)
            .Where(di => di.Status == ConnectionStatus.Active)
            .AsQueryable();

        if (interfaceType.HasValue)
        {
            products = products.Where(di => di.InterfaceType == interfaceType.Value);
        }

        var result = await products
            .OrderBy(di => di.InterfaceType)
            .ThenBy(di => di.Product.NameProduct)
            .ThenBy(di => di.Name)
            .Select(di => new InterfaceSearchResult
            {
                Id = di.Id,
                Name = di.Name,
                Description = di.Description,
                InterfaceType = di.InterfaceType,
                ConnectionStatus = di.Status,
                ProductName = di.Product.NameProduct,
                ProductId = di.ProductId
            })
            .ToListAsync();

        return result;
    }

    public async Task<List<InterfaceSearchResult>> SearchInterfacesAdvanced(string? productName, string? interfaceName,
        InterfaceType? interfaceType)
    {
        await using var context = await publicationContext.CreateDbContextAsync();

        var products = context.DataInterfaces
            .Include(p => p.Product)
            .Where(di => di.Status == ConnectionStatus.Active)
            .AsQueryable();

        if (!string.IsNullOrEmpty(productName))
        {
            products = products.Where(di => di.Product.NameProduct.Contains(productName));
        }

        if (!string.IsNullOrEmpty(interfaceName))
        {
            products = products.Where(di => di.Name.Contains(interfaceName));
        }

        if (interfaceType.HasValue)
        {
            products = products.Where(di => di.InterfaceType == interfaceType.Value);
        }

        var result = await products
            .OrderBy(di => di.Product.NameProduct)
            .ThenBy(di => di.Name)
            .Select(di => new InterfaceSearchResult
            {
                Id = di.Id,
                Name = di.Name,
                Description = di.Description,
                InterfaceType = di.InterfaceType,
                ConnectionStatus = di.Status,
                ProductName = di.Product.NameProduct,
                ProductId = di.ProductId
            })
            .ToListAsync();

        return result;
    }

    public async Task<InterfaceDetailsDto?> GetInterfaceByIdAsync(int id)
    {
        await using var context = await publicationContext.CreateDbContextAsync();

        var interface_ = await context.DataInterfaces
            .Include(di => di.Product)
            .FirstOrDefaultAsync(di => di.Id == id && di.Status == ConnectionStatus.Active);

        if (interface_ == null)
            return null;

        var dto = new InterfaceDetailsDto
        {
            Id = interface_.Id,
            Name = interface_.Name,
            Description = interface_.Description,
            // InterfaceType = interface_.InterfaceType,
            // Status = interface_.Status,
            ProductId = interface_.ProductId,
            ProductName = interface_.Product?.NameProduct ?? "Unknown"
        };

        // Заполняем специфичные поля в зависимости от типа
        switch (interface_)
        {
            case KafkaInterface kafka:
                dto.BootstrapServers = kafka.BootstrapServers;
                dto.TopicName = kafka.TopicName;
                dto.Username = kafka.Username;
                dto.Password = kafka.Password;
                break;

            case ApiInterface api:
                dto.Host = api.Host;
                dto.Port = api.Port;
                dto.Endpoint = api.Endpoint;
                dto.Token = api.Token;
                dto.Username = api.Username;
                dto.Password = api.Password;
                break;

            case DatabaseInterface db:
                dto.Host = db.Host;
                dto.Port = db.Port;
                dto.DatabaseName = db.DatabaseName;
                dto.Scheme = db.Scheme;
                dto.Username = db.Username;
                dto.Password = db.Password;
                break;
        }

        return dto;
    }
}