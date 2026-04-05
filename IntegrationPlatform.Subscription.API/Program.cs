using IntegrationPlatform.Subscription.API.Interfaces;
using IntegrationPlatform.Subscription.API.Services;
using IntegrationPlatform.Subscription.DataAccess;
using IntegrationPlatform.Subscription.API.Metrics;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
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

builder.Services.AddSubscriptionDbContext(
    builder.Configuration.GetConnectionString("SubscriptionDbContext"));

builder.Services.AddDbContextFactory<SubscriptionDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("SubscriptionDbContext"));
});

builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

builder.Services.AddHttpClient("SearchApi", client =>
{
    client.BaseAddress = new Uri("http://search-api:8080");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("EngineApi", client =>
{
    client.BaseAddress = new Uri("http://integration-engine:8080");
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SubscriptionDbContext>();
    dbContext.Database.EnsureCreated();

    UpdateSubscriptionMetrics(dbContext);
}

_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(30000);
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SubscriptionDbContext>();
            UpdateSubscriptionMetrics(dbContext);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating metrics: {ex.Message}");
        }
    }
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseAuthorization();

app.MapControllers();
app.UseStaticFiles();

app.Urls.Add("http://0.0.0.0:8080");

app.UseHttpMetrics();
app.MapMetrics();

app.Run();


void UpdateSubscriptionMetrics(SubscriptionDbContext dbContext)
{
    var totalProducts = dbContext.Products.Count();
    var totalInterfaces = dbContext.DataInterfaces.Count();
    var activeInterfaces =
        dbContext.DataInterfaces.Count(d => d.Status == IntegrationPlatform.Common.Enums.ConnectionStatus.Active);
    var draftInterfaces =
        dbContext.DataInterfaces.Count(d => d.Status == IntegrationPlatform.Common.Enums.ConnectionStatus.Draft);
    var deprecatedInterfaces =
        dbContext.DataInterfaces.Count(d => d.Status == IntegrationPlatform.Common.Enums.ConnectionStatus.Deprecated);

    var apiInterfaces =
        dbContext.DataInterfaces.Count(d => d.InterfaceType == IntegrationPlatform.Common.Enums.InterfaceType.Api);
    var kafkaInterfaces =
        dbContext.DataInterfaces.Count(d => d.InterfaceType == IntegrationPlatform.Common.Enums.InterfaceType.Kafka);
    var databaseInterfaces =
        dbContext.DataInterfaces.Count(d => d.InterfaceType == IntegrationPlatform.Common.Enums.InterfaceType.Db);

    var totalConfigs = dbContext.OrchestrationConfigs.Count();

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