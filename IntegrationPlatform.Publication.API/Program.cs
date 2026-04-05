using IntegrationPlatform.Publication.API.Interfaces;
using IntegrationPlatform.Publication.API.Metrics;
using IntegrationPlatform.Publication.API.Services;
using IntegrationPlatform.Publication.DataAccess;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
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

builder.Services.AddPublicationDbContext(
    builder.Configuration.GetConnectionString("PublicationDbContext"));

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

    UpdatePublicationMetrics(dbContext);
}

_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(30000);
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PublicationDbContext>();
            UpdatePublicationMetrics(dbContext);
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
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Urls.Add("http://0.0.0.0:8080");

app.UseHttpMetrics();
app.MapMetrics();

app.Run();

void UpdatePublicationMetrics(PublicationDbContext dbContext)
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