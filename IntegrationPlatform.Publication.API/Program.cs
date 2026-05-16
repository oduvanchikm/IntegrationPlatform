using IntegrationPlatform.Publication.API.Interfaces;
using IntegrationPlatform.Publication.API.Metrics;
using IntegrationPlatform.Publication.API.Services;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/publication-api-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddDbContextFactory<PublicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("PublicationDbContext"));
});


builder.Services.AddScoped<IPublicationService, PublicationService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PublicationDbContext>();
    dbContext.Database.EnsureCreated();

    await UpdatePublicationMetricsAsync(dbContext, CancellationToken.None);
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
            var dbContext = scope.ServiceProvider.GetRequiredService<PublicationDbContext>();
            await UpdatePublicationMetricsAsync(dbContext, cts.Token);
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

async Task UpdatePublicationMetricsAsync(PublicationDbContext dbContext, CancellationToken cancellationToken = default)
{
    var totalProducts = await dbContext.Products.CountAsync(cancellationToken);
    var totalInterfaces = await dbContext.DataInterfaces.CountAsync(cancellationToken);
    var activeInterfaces =
        await dbContext.DataInterfaces.CountAsync(d =>
            d.Status == IntegrationPlatform.Common.Enums.ConnectionStatus.Active, cancellationToken);
    var draftInterfaces =
        await dbContext.DataInterfaces.CountAsync(d =>
            d.Status == IntegrationPlatform.Common.Enums.ConnectionStatus.Draft, cancellationToken);
    var deprecatedInterfaces = await dbContext.DataInterfaces.CountAsync(d =>
        d.Status == IntegrationPlatform.Common.Enums.ConnectionStatus.Deprecated, cancellationToken);
    var apiInterfaces =
        await dbContext.DataInterfaces.CountAsync(d =>
            d.InterfaceType == IntegrationPlatform.Common.Enums.InterfaceType.Api, cancellationToken);
    var kafkaInterfaces =
        await dbContext.DataInterfaces.CountAsync(d =>
            d.InterfaceType == IntegrationPlatform.Common.Enums.InterfaceType.Kafka, cancellationToken);
    var databaseInterfaces =
        await dbContext.DataInterfaces.CountAsync(d =>
            d.InterfaceType == IntegrationPlatform.Common.Enums.InterfaceType.Db, cancellationToken);

    PublicationMetrics.TotalProducts.Set(totalProducts);
    PublicationMetrics.TotalInterfaces.Set(totalInterfaces);
    PublicationMetrics.ActiveSourceInterfaces.Set(activeInterfaces);
    PublicationMetrics.DraftInterfaces.Set(draftInterfaces);
    PublicationMetrics.DeprecatedInterfaces.Set(deprecatedInterfaces);
    PublicationMetrics.ApiInterfaces.Set(apiInterfaces);
    PublicationMetrics.KafkaInterfaces.Set(kafkaInterfaces);
    PublicationMetrics.DatabaseInterfaces.Set(databaseInterfaces);

    Console.WriteLine(
        $"Publication Metrics updated: Products={totalProducts}, Interfaces={totalInterfaces}, Active={activeInterfaces}");
}