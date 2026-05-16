using System.Collections.Concurrent;
using NCrontab;

namespace IntegrationPlatform.Engine.Applications.Orchestration.Handlers;

public abstract class BaseBatchHandler(ILogger logger)
{
    private string GetTaskKey(int sourceId, int targetId) => $"{sourceId}:{targetId}";

    protected async Task RunScheduledAsync(
        int sourceId,
        int targetId,
        string scheduleCron,
        Func<CancellationToken, Task> executeAction,
        CancellationToken externalCancellationToken = default)
    {
        var taskKey = GetTaskKey(sourceId, targetId);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                var schedule = CrontabSchedule.Parse(scheduleCron);
                var nextRun = schedule.GetNextOccurrence(DateTime.UtcNow);

                while (!cts.Token.IsCancellationRequested)
                {
                    var delay = nextRun - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cts.Token);
                    }

                    await executeAction(cts.Token);
                    nextRun = schedule.GetNextOccurrence(DateTime.UtcNow);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Task for key {TaskKey} was cancelled", taskKey);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in scheduled task for key {TaskKey}", taskKey);
            }
            finally
            {
                cts.Dispose();
            }
        }, cts.Token);

        logger.LogInformation("Scheduled task started with cron: {Cron}, key: {TaskKey}", scheduleCron, taskKey);
    }
}