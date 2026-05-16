using System.Collections.Concurrent;
using IntegrationPlatform.Common.Enums;
using IntegrationPlatform.Common.Models;
using IntegrationPlatform.Engine.Applications.Orchestration.Handlers;
using IntegrationPlatform.Engine.Metrics;
using IntegrationPlatform.Publication.DataAccess.DatabaseConnection;
using IntegrationPlatform.Subscription.DataAccess.DatabaseConnection;
using Microsoft.EntityFrameworkCore;

namespace IntegrationPlatform.Engine.Applications.Orchestration;

public class OrchestrationService(
    ILogger<OrchestrationService> logger,
    IDbContextFactory<PublicationDbContext> publicationDbContext,
    IDbContextFactory<SubscriptionDbContext> subscriptionDbContext,
    IServiceProvider serviceProvider)
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeTasks = new();

    private static string GetTaskKey(int sourceId, int targetId, string pattern)
        => $"{sourceId}_{targetId}_{pattern}";

    public async Task HandleNewOrchestration(OrchestrationConfigModel config)
    {
        logger.LogInformation("========== HANDLE NEW ORCHESTRATION ==========");
        logger.LogInformation("Processing config ID: {ConfigId}", config.Id);
        logger.LogInformation("Publication ID: {PubId}, Subscription ID: {SubId}, Pattern: {Pattern}",
            config.InterfacePublicationId, config.InterfaceSubscriptionId, config.IntegrationPattern);

        await using var publicationDb = await publicationDbContext.CreateDbContextAsync();
        await using var subscriptionDb = await subscriptionDbContext.CreateDbContextAsync();

        logger.LogInformation("Fetching publication interface with ID: {PubId}", config.InterfacePublicationId);

        var publicationInterface = await publicationDb.DataInterfaces
            .Include(d => d.Product)
            .FirstOrDefaultAsync(d => d.Id == config.InterfacePublicationId);

        if (publicationInterface == null)
        {
            logger.LogError("Publication interface {PubId} NOT FOUND in database!", config.InterfacePublicationId);
            return;
        }

        DataInterface enrichedPublication = publicationInterface;
        switch (publicationInterface.InterfaceType)
        {
            case InterfaceType.Api:
                var apiPublication =
                    await publicationDb.ApiInterfaces.FirstOrDefaultAsync(d => d.Id == enrichedPublication.Id);
                if (apiPublication != null)
                {
                    enrichedPublication = apiPublication;
                }

                break;
            case InterfaceType.Kafka:
                var kafkaPublication =
                    await publicationDb.KafkaInterfaces.FirstOrDefaultAsync(d => d.Id == enrichedPublication.Id);
                if (kafkaPublication != null)
                {
                    enrichedPublication = kafkaPublication;
                }

                break;
            case InterfaceType.Db:
                var dbPublication =
                    await publicationDb.DatabaseInterfaces.FirstOrDefaultAsync(d => d.Id == enrichedPublication.Id);
                if (dbPublication != null)
                {
                    enrichedPublication = dbPublication;
                }

                break;
        }

        logger.LogInformation("Fetching subscription interface with ID: {SubId}", config.InterfaceSubscriptionId);

        var subscriptionInterface = await subscriptionDb.DataInterfaces
            .Include(d => d.Product)
            .Include(d => d.OrchestrationConfig)
            .FirstOrDefaultAsync(d => d.Id == config.InterfaceSubscriptionId);

        if (subscriptionInterface == null)
        {
            logger.LogError("Subscription interface {SubId} NOT FOUND in database!", config.InterfaceSubscriptionId);
            return;
        }

        DataInterface enrichedSubscription = subscriptionInterface;
        switch (enrichedSubscription.InterfaceType)
        {
            case InterfaceType.Api:
                var apiSubscription =
                    await subscriptionDb.ApiInterfaces.FirstOrDefaultAsync(d => d.Id == enrichedSubscription.Id);
                if (apiSubscription != null)
                {
                    enrichedSubscription = apiSubscription;
                }

                break;
            case InterfaceType.Kafka:
                var kafkaSubscription =
                    await subscriptionDb.KafkaInterfaces.FirstOrDefaultAsync(d => d.Id == enrichedSubscription.Id);
                if (kafkaSubscription != null)
                {
                    enrichedSubscription = kafkaSubscription;
                }

                break;
            case InterfaceType.Db:
                var dbSubscription =
                    await subscriptionDb.DatabaseInterfaces.FirstOrDefaultAsync(d => d.Id == enrichedSubscription.Id);
                if (dbSubscription != null)
                {
                    enrichedSubscription = dbSubscription;
                }

                break;
        }

        logger.LogInformation("Publication interface found: ID={Id}, Name={Name}, Type={Type}",
            publicationInterface.Id, publicationInterface.Name, publicationInterface.InterfaceType);
        logger.LogInformation("Subscription interface found: ID={Id}, Name={Name}, Type={Type}",
            subscriptionInterface.Id, subscriptionInterface.Name, subscriptionInterface.InterfaceType);

        await HandleIntegration(config.IntegrationPattern, enrichedPublication, enrichedSubscription, config);

        logger.LogInformation("========== HANDLE NEW ORCHESTRATION END ==========");
    }

    private async Task HandleIntegration(string integrationPattern, DataInterface publicationInterface,
        DataInterface subscriptionInterface, OrchestrationConfigModel config)
    {
        logger.LogInformation("HandleIntegration called with pattern: {Pattern}", integrationPattern);

        var taskKey = GetTaskKey(
            publicationInterface.Id,
            subscriptionInterface.Id,
            integrationPattern);

        if (_activeTasks.TryRemove(taskKey, out var existingCts))
        {
            logger.LogInformation("Stopping existing task for key {TaskKey}", taskKey);
            existingCts.Cancel();
            existingCts.Dispose();
            await Task.Delay(1000);
            EngineMetrics.ActiveTasks.Dec();
        }

        var cts = new CancellationTokenSource();
        _activeTasks[taskKey] = cts;
        
        EngineMetrics.ActiveTasks.Inc();

        try
        {
            switch (integrationPattern)
            {
                case "ApiToApi":
                    logger.LogInformation("========== EXECUTING API TO API ==========");
                    logger.LogInformation("Creating ApiToApiHandler");

                    var apiToApiHandler = ActivatorUtilities.CreateInstance<ApiToApiHandler>(serviceProvider);

                    logger.LogInformation("Calling ApiToApiHandler.ExecuteAsync");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            await apiToApiHandler.ExecuteAsync(publicationInterface, subscriptionInterface,
                                config.ScheduleCron, linkedCts.Token);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "ApiToApiHandler.ExecuteAsync failed");
                        }
                    }, cts.Token);

                    logger.LogInformation("========== API TO API EXECUTION COMPLETE ==========");
                    break;

                case "ApiToDatabase":
                    logger.LogInformation("========== EXECUTING API TO DATABASE ==========");
                    logger.LogInformation("Creating ApiToDatabaseHandler");

                    var apiToDatabaseHandler = ActivatorUtilities.CreateInstance<ApiToDatabaseHandler>(serviceProvider);

                    logger.LogInformation("Calling ApiToDatabaseHandler.ExecuteAsync");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            await apiToDatabaseHandler.ExecuteAsync(publicationInterface, subscriptionInterface,
                                config.ScheduleCron, linkedCts.Token);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "ApiToDatabaseHandler.ExecuteAsync failed");
                        }
                    }, cts.Token);

                    logger.LogInformation("========== API TO DATABASE EXECUTION COMPLETE ==========");
                    break;

                case "ApiToKafka":
                    logger.LogInformation("========== EXECUTING API TO KAFKA ==========");
                    logger.LogInformation("Creating ApiToKafkaHandler");

                    var apiToKafkaHandler = ActivatorUtilities.CreateInstance<ApiToKafkaHandler>(serviceProvider);

                    logger.LogInformation("Calling ApiToKafkaHandler.ExecuteAsync");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            await apiToKafkaHandler.ExecuteAsync(publicationInterface, subscriptionInterface,
                                config.ScheduleCron, linkedCts.Token);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "ApiToKafkaHandler.ExecuteAsync failed");
                        }
                    }, cts.Token);

                    logger.LogInformation("========== API TO KAFKA EXECUTION COMPLETE ==========");
                    break;

                case "KafkaToApi":
                    logger.LogInformation("========== EXECUTING KAFKA TO API ==========");
                    logger.LogInformation("Creating KafkaToApiHandler");

                    var kafkaToApiHandler = ActivatorUtilities.CreateInstance<KafkaToApiHandler>(serviceProvider);

                    logger.LogInformation("Calling KafkaToApiHandler.ExecuteAsync");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            await kafkaToApiHandler.ExecuteAsync(publicationInterface, subscriptionInterface,
                                linkedCts.Token);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "KafkaToApiHandler.ExecuteAsync failed");
                        }
                    }, cts.Token);

                    logger.LogInformation("========== KAFKA TO API EXECUTION COMPLETE ==========");
                    break;

                case "KafkaToKafka":
                    logger.LogInformation("========== EXECUTING KAFKA TO KAFKA ==========");
                    logger.LogInformation("Creating KafkaToKafkaHandler");

                    var kafkaToKafkaHandler = ActivatorUtilities.CreateInstance<KafkaToKafkaHandler>(serviceProvider);

                    logger.LogInformation("Calling KafkaToKafkaHandler.ExecuteAsync");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            await kafkaToKafkaHandler.ExecuteAsync(publicationInterface, subscriptionInterface,
                                linkedCts.Token);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "KafkaToKafkaHandler.ExecuteAsync failed");
                        }
                    }, cts.Token);

                    logger.LogInformation("========== KAFKA TO KAFKA EXECUTION COMPLETE ==========");
                    break;

                case "KafkaToDatabase":
                    logger.LogInformation("========== EXECUTING KAFKA TO DATABASE ==========");
                    logger.LogInformation("Creating KafkaToDatabaseHandler");

                    var kafkaToDatabaseHandler =
                        ActivatorUtilities.CreateInstance<KafkaToDatabaseHandler>(serviceProvider);

                    logger.LogInformation("Calling KafkaToDatabaseHandler.ExecuteAsync");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            await kafkaToDatabaseHandler.ExecuteAsync(publicationInterface, subscriptionInterface,
                                linkedCts.Token);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "KafkaToDatabaseHandler.ExecuteAsync failed");
                        }
                    }, cts.Token);

                    logger.LogInformation("========== KAFKA TO DATABASE EXECUTION COMPLETE ==========");
                    break;

                case "DatabaseToApi":
                    logger.LogInformation("========== EXECUTING DATABASE TO API ==========");
                    logger.LogInformation("Creating DatabaseToApiHandler");

                    var databaseToApiHandler = ActivatorUtilities.CreateInstance<DatabaseToApiHandler>(serviceProvider);

                    logger.LogInformation("Calling DatabaseToApiHandler.ExecuteAsync");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            await databaseToApiHandler.ExecuteAsync(publicationInterface, subscriptionInterface,
                                config.ScheduleCron, linkedCts.Token);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "DatabaseToApiHandler.ExecuteAsync failed");
                        }
                    }, cts.Token);

                    logger.LogInformation("========== DATABASE TO API EXECUTION COMPLETE ==========");
                    break;

                case "DatabaseToKafka":
                    logger.LogInformation("========== EXECUTING DATABASE TO KAFKA ==========");
                    logger.LogInformation("Creating DatabaseToKafkaHandler");

                    var databaseToKafkaHandler =
                        ActivatorUtilities.CreateInstance<DatabaseToKafkaHandler>(serviceProvider);

                    logger.LogInformation("Calling DatabaseToKafkaHandler.ExecuteAsync");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            await databaseToKafkaHandler.ExecuteAsync(publicationInterface, subscriptionInterface,
                                config.ScheduleCron, linkedCts.Token);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "DatabaseToKafkaHandler.ExecuteAsync failed");
                        }
                    }, cts.Token);

                    logger.LogInformation("========== DATABASE TO KAFKA EXECUTION COMPLETE ==========");
                    break;

                case "DatabaseToDatabase":
                    logger.LogInformation("========== EXECUTING DATABASE TO DATABASE ==========");
                    logger.LogInformation("Creating DatabaseToDatabaseHandler");

                    var databaseToDatabaseHandler =
                        ActivatorUtilities.CreateInstance<DatabaseToDatabaseHandler>(serviceProvider);

                    logger.LogInformation("Calling DatabaseToDatabaseHandler.ExecuteAsync");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            await databaseToDatabaseHandler.ExecuteAsync(publicationInterface, subscriptionInterface,
                                config.ScheduleCron, linkedCts.Token);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "DatabaseToDatabaseHandler.ExecuteAsync failed");
                        }
                    }, cts.Token);

                    logger.LogInformation("========== DATABASE TO DATABASE EXECUTION COMPLETE ==========");
                    break;

                default:
                    logger.LogInformation("========== EXECUTING default ==========");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in HandleIntegration for pattern {Pattern}", integrationPattern);
            EngineMetrics.ActiveTasks.Dec();
        }
    }
}