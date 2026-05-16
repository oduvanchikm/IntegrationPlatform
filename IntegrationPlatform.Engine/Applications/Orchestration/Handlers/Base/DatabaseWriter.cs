using System.Collections.Concurrent;
using IntegrationPlatform.Common.Models;
using Npgsql;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class DatabaseWriter(ILogger<DatabaseWriter> logger)
{
    private static readonly ConcurrentDictionary<string, bool> _tablesInitialized = new();

    public async Task WriteBatchAsync(DatabaseInterface dbInterface, List<string> messages)
    {
        if (!messages.Any())
        {
            logger.LogInformation("No messages to write to database");
            return;
        }

        var connString =
            $"Host={dbInterface.Host};Port={dbInterface.Port};Database={dbInterface.DatabaseName};Username={dbInterface.Username};Password={dbInterface.Password}";

        logger.LogInformation("Writing batch of {Count} messages to database: {Database}.{Schema}",
            messages.Count, dbInterface.DatabaseName, dbInterface.Scheme);

        try
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            
            var tableKey = $"{dbInterface.DatabaseName}:{dbInterface.Scheme}.data";

            if (!_tablesInitialized.ContainsKey(tableKey))
            {
                var createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS {dbInterface.Scheme}.data (
                        id SERIAL PRIMARY KEY,
                        payload JSONB NOT NULL,
                        created_at TIMESTAMP DEFAULT NOW()
                    )";
                await using var createCmd = new NpgsqlCommand(createTableSql, conn);
                await createCmd.ExecuteNonQueryAsync();
                _tablesInitialized[tableKey] = true;
                logger.LogInformation("Table {Schema}.data initialized", dbInterface.Scheme);
            }

            await using var writer = await conn.BeginBinaryImportAsync(
                $"COPY {dbInterface.Scheme}.data (payload) FROM STDIN (FORMAT BINARY)");

            foreach (var message in messages)
            {
                writer.StartRow();
                writer.Write(message, NpgsqlTypes.NpgsqlDbType.Jsonb);
            }

            await writer.CompleteAsync();
            logger.LogInformation("Successfully wrote {Count} messages using COPY", messages.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing batch to database");
            throw;
        }
    }

    public async Task WriteToDatabaseAsync(DatabaseInterface dbInterface, List<string> messages)
    {
        await WriteBatchAsync(dbInterface, messages);
    }
}