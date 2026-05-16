using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Subscription.API.Interfaces;
using IntegrationPlatform.Subscription.API.Services;
using IntegrationPlatform.Subscription.API.Metrics;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/subscription-api-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContextFactory<SubscriptionDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("SubscriptionDbContext"));
});

builder.Services.AddDbContextFactory<PublicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("PublicationDbContext"));
});

builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IInterfaceService, InterfaceService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SubscriptionDbContext>();
    dbContext.Database.EnsureCreated();

    await UpdateSubscriptionMetrics(dbContext, CancellationToken.None);
}

var cts = new CancellationTokenSource();
AppDomain.CurrentDomain.ProcessExit += (s, e) => cts.Cancel();

_ = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        await Task.Delay(30000, cts.Token);
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SubscriptionDbContext>();
            await UpdateSubscriptionMetrics(dbContext, cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating metrics: {ex.Message}");
        }
    }
}, cts.Token);

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseAuthorization();

app.MapControllers();

app.UseHttpMetrics();
app.MapMetrics();

await app.RunAsync();


async Task UpdateSubscriptionMetrics(SubscriptionDbContext dbContext, CancellationToken ct = default)
{
    var totalProducts = await dbContext.Products.CountAsync(ct);
    var totalInterfaces = await dbContext.DataInterfaces.CountAsync(ct);
    var activeInterfaces = await dbContext.DataInterfaces
        .CountAsync(d => d.Status == ConnectionStatus.Active, ct);
    var draftInterfaces = await dbContext.DataInterfaces
        .CountAsync(d => d.Status == ConnectionStatus.Draft, ct);
    var deprecatedInterfaces = await dbContext.DataInterfaces
        .CountAsync(d => d.Status == ConnectionStatus.Deprecated, ct);

    var apiInterfaces = await dbContext.DataInterfaces
        .CountAsync(d => d.InterfaceType == InterfaceType.Api, ct);
    var kafkaInterfaces = await dbContext.DataInterfaces
        .CountAsync(d => d.InterfaceType == InterfaceType.Kafka, ct);
    var databaseInterfaces = await dbContext.DataInterfaces
        .CountAsync(d => d.InterfaceType == InterfaceType.Db, ct);

    var totalConfigs = await dbContext.OrchestrationConfigs.CountAsync(ct);

    SubscriptionMetrics.TotalProducts.Set(totalProducts);
    SubscriptionMetrics.TotalInterfaces.Set(totalInterfaces);
    SubscriptionMetrics.ActiveSubscriptions.Set(activeInterfaces);
    SubscriptionMetrics.DraftInterfaces.Set(draftInterfaces);
    SubscriptionMetrics.DeprecatedInterfaces.Set(deprecatedInterfaces);
    SubscriptionMetrics.ApiInterfaces.Set(apiInterfaces);
    SubscriptionMetrics.KafkaInterfaces.Set(kafkaInterfaces);
    SubscriptionMetrics.DatabaseInterfaces.Set(databaseInterfaces);
    SubscriptionMetrics.TotalOrchestrationConfigs.Set(totalConfigs);

    Console.WriteLine(
        $"Subscription Metrics updated: Products={totalProducts}, Interfaces={totalInterfaces}, Active={activeInterfaces}, Configs={totalConfigs}");
}