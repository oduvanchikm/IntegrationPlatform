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

        var interfaces = context.DataInterfaces
            .Include(p => p.Product)
            .Where(di => di.Status == ConnectionStatus.Active)
            .AsQueryable();

        if (!string.IsNullOrEmpty(productName))
        {
            interfaces = interfaces.Where(di => di.Product.NameProduct.Contains(productName));
        }

        if (!string.IsNullOrEmpty(interfaceName))
        {
            interfaces = interfaces.Where(di => di.Name.Contains(interfaceName));
        }

        if (interfaceType.HasValue)
        {
            interfaces = interfaces.Where(di => di.InterfaceType == interfaceType.Value);
        }

        var baseResults = await interfaces.ToListAsync();
        var result = new List<InterfaceSearchResult>();

        foreach (var di in baseResults)
        {
            var searchResult = new InterfaceSearchResult
            {
                Id = di.Id,
                Name = di.Name,
                Description = di.Description,
                InterfaceType = di.InterfaceType,
                ConnectionStatus = di.Status,
                ProductName = di.Product?.NameProduct,
                ProductId = di.ProductId
            };

            switch (di.InterfaceType)
            {
                case InterfaceType.Api:
                    var apiData = await context.ApiInterfaces.FirstOrDefaultAsync(a => a.Id == di.Id);
                    if (apiData != null)
                    {
                        searchResult.Host = apiData.Host;
                        searchResult.Port = apiData.Port;
                        searchResult.Endpoint = apiData.Endpoint;
                    }

                    break;

                case InterfaceType.Kafka:
                    var kafkaData = await context.KafkaInterfaces
                        .FirstOrDefaultAsync(k => k.Id == di.Id);
                    if (kafkaData != null)
                    {
                        searchResult.BootstrapServers = kafkaData.BootstrapServers;
                        searchResult.TopicName = kafkaData.TopicName;
                    }

                    break;

                case InterfaceType.Db:
                    var dbData = await context.DatabaseInterfaces
                        .FirstOrDefaultAsync(d => d.Id == di.Id);
                    if (dbData != null)
                    {
                        searchResult.Host = dbData.Host;
                        searchResult.Port = dbData.Port;
                        searchResult.DatabaseName = dbData.DatabaseName;
                        searchResult.Scheme = dbData.Scheme;
                    }

                    break;
            }

            result.Add(searchResult);
        }

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
            InterfaceType = interface_.InterfaceType.ToString(),
            Status = interface_.Status.ToString(),
            ProductId = interface_.ProductId,
            ProductName = interface_.Product?.NameProduct ?? "Unknown"
        };

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