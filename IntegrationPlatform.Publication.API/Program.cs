using IntegrationPlatform.Publication.API.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContextFactory<PublicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PublicationDbContext"),
        x => x.MigrationsHistoryTable("__EFMigrationsHistory", "publication")
    ));

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