var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var sourceDataStore = new List<object>();
var targetDataStore = new List<object>();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
app.MapGet("/api/source-data", () =>
{
    var lastData = sourceDataStore.LastOrDefault();
    if (lastData == null)
    {
        return Results.Ok(new { message = "No data" });
    }

    return Results.Ok(lastData);
});

app.MapPost("/api/source-data", (dynamic data) =>
{
    sourceDataStore.Add(new
    {
        receivedAt = DateTime.UtcNow,
        data = data
    });
    Console.WriteLine($"[Source] Received: {data}");
    return Results.Ok(new { received = true, timestamp = DateTime.UtcNow });
});

app.MapGet("/api/target-data", () =>
{
    return Results.Ok(new
    {
        timestamp = DateTime.UtcNow,
        receivedData = targetDataStore,
        count = targetDataStore.Count
    });
});

app.MapPost("/api/target-data", (dynamic data) =>
{
    targetDataStore.Add(new
    {
        receivedAt = DateTime.UtcNow,
        data = data
    });
    Console.WriteLine($"[Target] Received: {data}");
    return Results.Ok(new { received = true, timestamp = DateTime.UtcNow });
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{port}");

Console.WriteLine($"Mock API running on port {port}");

app.Run();