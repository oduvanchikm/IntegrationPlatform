var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Хранилище для данных
var dataStore = new List<object>();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// ========== Source API ==========

// GET - получить данные
app.MapGet("/api/source-data", () =>
{
    return Results.Ok(new
    {
        timestamp = DateTime.UtcNow,
        data = dataStore.LastOrDefault(),
        message = "Data from source API"
    });
});

// POST - отправить данные
app.MapPost("/api/source-data", (dynamic data) =>
{
    dataStore.Add(new 
    { 
        receivedAt = DateTime.UtcNow, 
        data = data 
    });
    Console.WriteLine($"[Source] Received: {data}");
    return Results.Ok(new { received = true, timestamp = DateTime.UtcNow });
});

// ========== Target API ==========

// GET - получить все полученные данные
app.MapGet("/api/target-data", () =>
{
    return Results.Ok(new
    {
        timestamp = DateTime.UtcNow,
        receivedData = dataStore,
        count = dataStore.Count
    });
});

// POST - принять данные
app.MapPost("/api/target-data", (dynamic data) =>
{
    dataStore.Add(new 
    { 
        receivedAt = DateTime.UtcNow, 
        data = data 
    });
    Console.WriteLine($"[Target] Received: {data}");
    return Results.Ok(new { received = true, timestamp = DateTime.UtcNow });
});

// Получаем порт из переменной окружения или используем значение по умолчанию
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{port}"); 

Console.WriteLine($"Mock API running on port {port}");

app.Run();