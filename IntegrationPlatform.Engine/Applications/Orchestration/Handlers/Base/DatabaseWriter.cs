using IntegrationPlatform.Common.Models;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class DatabaseWriter(ILogger<DatabaseWriter> logger)
{
    public async Task WriteToDatabaseAsync(DatabaseInterface apiInterface, List<string> messages)
    {
    }
}