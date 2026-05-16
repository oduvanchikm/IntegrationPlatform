using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Search.API.DTO;
using IntegrationPlatform.Search.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Search.API.Services;

public class SearchService(IDbContextFactory<PublicationDbContext> publicationContext, ILogger<SearchService> logger) : ISearchService
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

        var query = from di in context.DataInterfaces
            join api in context.ApiInterfaces on di.Id equals api.Id into apiJoin
            from api in apiJoin.DefaultIfEmpty()
            join kafka in context.KafkaInterfaces on di.Id equals kafka.Id into kafkaJoin
            from kafka in kafkaJoin.DefaultIfEmpty()
            join db in context.DatabaseInterfaces on di.Id equals db.Id into dbJoin
            from db in dbJoin.DefaultIfEmpty()
            where di.Status == ConnectionStatus.Active
            select new { di, api, kafka, db };


        if (!string.IsNullOrEmpty(productName))
        {
            query = query.Where(x => x.di.Product.NameProduct.Contains(productName));
        }

        if (!string.IsNullOrEmpty(interfaceName))
        {
            query = query.Where(x => x.di.Name.Contains(interfaceName));
        }

        if (interfaceType.HasValue)
        {
            query = query.Where(x => x.di.InterfaceType == interfaceType.Value);
        }

        var results = await query
            .OrderBy(x => x.di.Product.NameProduct)
            .ThenBy(x => x.di.Name)
            .Select(x => new InterfaceSearchResult
            {
                Id = x.di.Id,
                Name = x.di.Name,
                Description = x.di.Description,
                InterfaceType = x.di.InterfaceType,
                ConnectionStatus = x.di.Status,
                ProductName = x.di.Product.NameProduct,
                ProductId = x.di.ProductId,
                Host = x.api != null ? x.api.Host : (x.db != null ? x.db.Host : null),
                Port = x.api != null ? x.api.Port : (x.db != null ? x.db.Port : null),
                Endpoint = x.api != null ? x.api.Endpoint : null,
                Token = x.api != null ? x.api.Token : null,
                BootstrapServers = x.kafka != null ? x.kafka.BootstrapServers : null,
                TopicName = x.kafka != null ? x.kafka.TopicName : null,
                DatabaseName = x.db != null ? x.db.DatabaseName : null,
                Scheme = x.db != null ? x.db.Scheme : null
            })
            .ToListAsync();

        return results;
    }

    public async Task<InterfaceDetailsDto?> GetInterfaceByIdAsync(int id)
    {
        await using var context = await publicationContext.CreateDbContextAsync();

        var result = await (from di in context.DataInterfaces
            join api in context.ApiInterfaces on di.Id equals api.Id into apiJoin
            from api in apiJoin.DefaultIfEmpty()
            join kafka in context.KafkaInterfaces on di.Id equals kafka.Id into kafkaJoin
            from kafka in kafkaJoin.DefaultIfEmpty()
            join db in context.DatabaseInterfaces on di.Id equals db.Id into dbJoin
            from db in dbJoin.DefaultIfEmpty()
            where di.Id == id && di.Status == ConnectionStatus.Active
            select new
            {
                di,
                api,
                kafka,
                db
            }).FirstOrDefaultAsync();

        if (result == null)
            return null;

        var dto = new InterfaceDetailsDto
        {
            Id = result.di.Id,
            Name = result.di.Name,
            Description = result.di.Description,
            InterfaceType = result.di.InterfaceType.ToString(),
            Status = result.di.Status.ToString(),
            ProductId = result.di.ProductId,
            ProductName = result.di.Product?.NameProduct ?? "Unknown"
        };

        if (result.api != null)
        {
            dto.Host = result.api.Host;
            dto.Port = result.api.Port;
            dto.Endpoint = result.api.Endpoint;
            dto.Token = result.api.Token;
            dto.Username = result.api.Username;
            dto.Password = result.api.Password;
        }
        else if (result.kafka != null)
        {
            dto.BootstrapServers = result.kafka.BootstrapServers;
            dto.TopicName = result.kafka.TopicName;
            dto.Username = result.kafka.Username;
            dto.Password = result.kafka.Password;
        }
        else if (result.db != null)
        {
            dto.Host = result.db.Host;
            dto.Port = result.db.Port;
            dto.DatabaseName = result.db.DatabaseName;
            dto.Scheme = result.db.Scheme;
            dto.Username = result.db.Username;
            dto.Password = result.db.Password;
        }

        return dto;
    }
    
    public async Task<bool> CheckDatabaseHealthAsync()
    {
        try
        {
            await using var context = await publicationContext.CreateDbContextAsync();
            var canConnect = await context.Database.CanConnectAsync();
            return canConnect;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
            return false;
        }
    }
}