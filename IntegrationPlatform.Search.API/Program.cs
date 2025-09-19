using IntegrationPlatform.Search.API.Interfaces;
using IntegrationPlatform.Publication.API.DatabaseConnection;
using IntegrationPlatform.Search.API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<PublicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DiscoveryDbContext")));

builder.Services.AddScoped<ISearchService, SearchService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();