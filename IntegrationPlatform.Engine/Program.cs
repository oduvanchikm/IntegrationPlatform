using IntegrationPlatform.Engine;
using IntegrationPlatform.Publication.DataAccess;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddPublicationDbContext(
    builder.Configuration.GetConnectionString("PublicationDbContext"));

builder.Services.AddDbContextFactory<PublicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("PublicationDbContext"));
});

builder.Services.AddSubscriptionDbContext(
    builder.Configuration.GetConnectionString("SubscriptionDbContext"));

builder.Services.AddDbContextFactory<SubscriptionDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("SubscriptionDbContext"));
});

var host = builder.Build();
host.Run();
