using IntegrationPlatform.Publication.API.Interfaces;
using IntegrationPlatform.Publication.API.Services;
using IntegrationPlatform.Publication.DataAccess;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;
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
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Urls.Add("http://0.0.0.0:8080");

app.Run();