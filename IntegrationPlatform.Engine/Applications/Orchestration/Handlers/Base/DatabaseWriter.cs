using IntegrationPlatform.Common.Models;
using Npgsql;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class DatabaseWriter(ILogger<DatabaseWriter> logger)
{
    
    private static bool _tableInitialized = false;

    /// <summary>
    /// Массовая вставка данных через COPY (быстро)
    /// </summary>
    public async Task WriteBatchAsync(DatabaseInterface dbInterface, List<string> messages)
    {
        if (!messages.Any())
        {
            logger.LogInformation("No messages to write to database");
            return;
        }

        var connString = $"Host={dbInterface.Host};Port={dbInterface.Port};Database={dbInterface.DatabaseName};Username={dbInterface.Username};Password={dbInterface.Password}";
        
        logger.LogInformation("Writing batch of {Count} messages to database: {Database}.{Schema}", 
            messages.Count, dbInterface.DatabaseName, dbInterface.Scheme);

        try
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // Инициализация таблицы (один раз)
            if (!_tableInitialized)
            {
                var createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS {dbInterface.Scheme}.data (
                        id SERIAL PRIMARY KEY,
                        payload JSONB NOT NULL,
                        created_at TIMESTAMP DEFAULT NOW()
                    )";
                await using var createCmd = new NpgsqlCommand(createTableSql, conn);
                await createCmd.ExecuteNonQueryAsync();
                _tableInitialized = true;
                logger.LogInformation("Table {Schema}.data initialized", dbInterface.Scheme);
            }

            // Используем COPY для массовой вставки
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

    /// <summary>
    /// Одиночная вставка (для совместимости, использует COPY)
    /// </summary>
    public async Task WriteToDatabaseAsync(DatabaseInterface dbInterface, List<string> messages)
    {
        await WriteBatchAsync(dbInterface, messages);
    }
    // private static bool _tableCreated = false;
    //
    // public async Task WriteToDatabaseAsync(DatabaseInterface dbInterface, List<string> messages)
    // {
    //     if (!messages.Any())
    //     {
    //         logger.LogInformation("No messages to write to database");
    //         return;
    //     }
    //
    //     var connString =
    //         $"Host={dbInterface.Host};Port={dbInterface.Port};Database={dbInterface.DatabaseName};Username={dbInterface.Username};Password={dbInterface.Password}";
    //
    //     logger.LogInformation("Writing to database: {Database}.{Schema}", dbInterface.DatabaseName, dbInterface.Scheme);
    //
    //     try
    //     {
    //         await using var conn = new NpgsqlConnection(connString);
    //         await conn.OpenAsync();
    //
    //         if (!_tableCreated)
    //         {
    //             var createTableSql = $@"
    //                 CREATE TABLE IF NOT EXISTS {dbInterface.Scheme}.data (
    //                     id SERIAL PRIMARY KEY,
    //                     message TEXT NOT NULL,
    //                     created_at TIMESTAMP DEFAULT NOW()
    //                 )";
    //             await using var createCmd = new NpgsqlCommand(createTableSql, conn);
    //             await createCmd.ExecuteNonQueryAsync();
    //             _tableCreated = true;
    //             logger.LogInformation("Table {Schema}.data created", dbInterface.Scheme);
    //         }
    //
    //         var successCount = 0;
    //         var failCount = 0;
    //
    //         await using var transaction = await conn.BeginTransactionAsync();
    //
    //         foreach (var message in messages)
    //         {
    //             try
    //             {
    //                 var insertSql = $"INSERT INTO {dbInterface.Scheme}.data (payload) VALUES (@payload)";
    //                 await using var insertCmd = new NpgsqlCommand(insertSql, conn);
    //                 insertCmd.Parameters.AddWithValue("payload", NpgsqlTypes.NpgsqlDbType.Jsonb, message);
    //                 await insertCmd.ExecuteNonQueryAsync();
    //                 successCount++;
    //             }
    //             catch (Exception ex)
    //             {
    //                 failCount++;
    //                 logger.LogError(ex, "Failed to write message to database");
    //             }
    //         }
    //
    //         await transaction.CommitAsync();
    //
    //         logger.LogInformation("Successfully wrote {SuccessCount}/{TotalCount} messages to database",
    //             successCount, messages.Count);
    //
    //         if (failCount > 0)
    //         {
    //             logger.LogWarning("Failed to write {FailCount} messages", failCount);
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         logger.LogError(ex, "Error writing to database");
    //         throw;
    //     }
    // }
}