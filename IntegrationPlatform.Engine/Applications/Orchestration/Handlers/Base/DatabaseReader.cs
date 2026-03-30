using System.Runtime.CompilerServices;
using System.Text.Json;
using IntegrationPlatform.Common.Models;
using Npgsql;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers.Base;

public class DatabaseReader(ILogger<DatabaseReader> logger)
{
    public async Task ReadInBatchesAsync(
        DatabaseInterface dbInterface,
        Func<List<string>, Task> onBatch,
        int batchSize = 1000,
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var connString = $"Host={dbInterface.Host};Port={dbInterface.Port};Database={dbInterface.DatabaseName};Username={dbInterface.Username};Password={dbInterface.Password}";

        logger.LogInformation("Reading from database: {Database}.{Schema} in batches of {BatchSize}",
            dbInterface.DatabaseName, dbInterface.Scheme, batchSize);

        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(cancellationToken);

        var lastId = 0;
        var batchNumber = 0;
        var hasMore = true;

        var sql = query ?? $@"
            SELECT * FROM {dbInterface.Scheme}.data 
            WHERE id > @lastId 
            ORDER BY id 
            LIMIT @batchSize";

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            batchNumber++;
            var batch = new List<string>();

            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("lastId", lastId);
                cmd.Parameters.AddWithValue("batchSize", batchSize);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        row[reader.GetName(i)] = value;
                    }
                    
                    var json = JsonSerializer.Serialize(row);
                    batch.Add(json);
                    
                    // Обновляем lastId — правильно извлекаем значение id
                    if (reader.GetName(0) == "id")
                    {
                        var idValue = reader.GetValue(0);
                        lastId = Convert.ToInt32(idValue);
                    }
                }

                if (batch.Any())
                {
                    logger.LogDebug("Read batch {BatchNumber}: {Count} records, lastId={LastId}", 
                        batchNumber, batch.Count, lastId);
                    await onBatch(batch);
                }
                else
                {
                    hasMore = false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading batch {BatchNumber}", batchNumber);
                throw;
            }
        }

        logger.LogInformation("Completed reading from database: {TotalBatches} batches, lastId={LastId}", 
            batchNumber, lastId);
    }

    private async Task<List<string>?> GetBatchAsync(
        NpgsqlConnection conn,
        string sql,
        int lastId,
        int batchSize,
        int batchNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var batch = new List<string>();

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("lastId", lastId);
            cmd.Parameters.AddWithValue("batchSize", batchSize);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }

                batch.Add(JsonSerializer.Serialize(row));
            }

            return batch;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading batch {BatchNumber}", batchNumber);
            return null;
        }
    }

    public async Task<List<string>> ReadFromDatabaseAsync(DatabaseInterface dbInterface, string? query = null)
    {
        var results = new List<string>();
        
        await ReadInBatchesAsync(dbInterface, async (batch) =>
        {
            results.AddRange(batch);
        }, batchSize: 1000, query: query);
        
        return results;
    }

    // public async Task<List<string>> ReadFromDatabaseAsync(DatabaseInterface dbInterface, string query = null)
    // {
    //     var results = new List<string>();
    //     var connString =
    //         $"Host={dbInterface.Host};Port={dbInterface.Port};Database={dbInterface.DatabaseName};Username={dbInterface.Username};Password={dbInterface.Password}";
    //
    //     logger.LogInformation("Reading from database: {Database}.{Schema}", dbInterface.DatabaseName,
    //         dbInterface.Scheme);
    //
    //     try
    //     {
    //         await using var conn = new NpgsqlConnection(connString);
    //         await conn.OpenAsync();
    //
    //         var sql = query ?? $"SELECT * FROM {dbInterface.Scheme}.data ORDER BY created_at DESC LIMIT 100";
    //         await using var cmd = new NpgsqlCommand(sql, conn);
    //         await using var reader = await cmd.ExecuteReaderAsync();
    //
    //         while (await reader.ReadAsync())
    //         {
    //             var row = new Dictionary<string, object>();
    //             for (int i = 0; i < reader.FieldCount; i++)
    //             {
    //                 row[reader.GetName(i)] = reader.GetValue(i);
    //             }
    //
    //             results.Add(JsonSerializer.Serialize(row));
    //         }
    //
    //         logger.LogInformation("Read {Count} records from database", results.Count);
    //         return results;
    //     }
    //     catch (Exception ex)
    //     {
    //         logger.LogError(ex, "Error reading from database");
    //         throw;
    //     }
    // }
}