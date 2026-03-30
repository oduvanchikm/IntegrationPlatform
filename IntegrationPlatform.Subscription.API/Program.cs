using IntegrationPlatform.Subscription.API.Interfaces;
using IntegrationPlatform.Subscription.API.Services;
using IntegrationPlatform.Subscription.DataAccess;
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
}

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
app.UseMetricServer();

app.Run();