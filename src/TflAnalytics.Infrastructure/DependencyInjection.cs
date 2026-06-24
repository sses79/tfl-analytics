using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Cosmos;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Application.Ingestion;
using TflAnalytics.Application.Messaging;
using TflAnalytics.Application.Processing;
using TflAnalytics.Application.Realtime;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Infrastructure.Alerts;
using TflAnalytics.Infrastructure.Messaging;
using TflAnalytics.Infrastructure.Processing;
using TflAnalytics.Infrastructure.Realtime;
using TflAnalytics.Infrastructure.Tfl;

namespace TflAnalytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<TflApiOptions>()
            .Bind(configuration.GetSection(TflApiOptions.SectionName));
        services
            .AddOptions<EventHubsOptions>()
            .Bind(configuration.GetSection(EventHubsOptions.SectionName));

        services.AddHttpClient<ITflApiClient, TflApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<TflApiOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddSingleton(
            configuration.GetSection(IngestionOptions.SectionName).Get<IngestionOptions>()
            ?? new IngestionOptions());
        services.AddSingleton(
            configuration.GetSection(ArrivalOptions.SectionName).Get<ArrivalOptions>()
            ?? new ArrivalOptions());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<EventHubsOptions>>()
                .Value;

            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return new EventHubProducerClient(
                    options.ConnectionString,
                    options.EventHubName);
            }

            if (string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace))
            {
                throw new InvalidOperationException(
                    "Configure EventHubs:ConnectionString locally or "
                    + "EventHubs:FullyQualifiedNamespace in Azure.");
            }

            return new EventHubProducerClient(
                options.FullyQualifiedNamespace,
                options.EventHubName,
                new DefaultAzureCredential());
        });
        services.AddSingleton<IEventPublisher, EventHubsEventPublisher>();
        services.AddSingleton<IIngestionPoller, IngestionPoller>();

        return services;
    }

    public static IServiceCollection AddProcessingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ProcessingStorageOptions>()
            .Bind(configuration.GetSection(ProcessingStorageOptions.SectionName));
        services
            .AddOptions<CosmosOptions>()
            .Bind(configuration.GetSection(CosmosOptions.SectionName));
        services
            .AddOptions<AlertStorageOptions>()
            .Bind(configuration.GetSection(AlertStorageOptions.SectionName));
        services.AddSingleton(
            configuration.GetSection(AlertOptions.SectionName).Get<AlertOptions>()
            ?? new AlertOptions());
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<ProcessingStorageOptions>>()
                .Value;
            var serviceClient = !string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new BlobServiceClient(options.ConnectionString)
                : new BlobServiceClient(
                    new Uri(
                        $"https://{RequireAccountName(options.AccountName)}.blob.core.windows.net"),
                    new DefaultAzureCredential());

            return serviceClient.GetBlobContainerClient(options.RawContainerName);
        });
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<ProcessingStorageOptions>>()
                .Value;
            var clientOptions = new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            };
            var serviceClient = !string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new QueueServiceClient(options.ConnectionString, clientOptions)
                : new QueueServiceClient(
                    new Uri(
                        $"https://{RequireAccountName(options.AccountName)}.queue.core.windows.net"),
                    new DefaultAzureCredential(),
                    clientOptions);

            return serviceClient.GetQueueClient(options.QueueName);
        });
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<ProcessingStorageOptions>>()
                .Value;
            return !string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new TableServiceClient(options.ConnectionString)
                : new TableServiceClient(
                    new Uri(
                        $"https://{RequireAccountName(options.AccountName)}.table.core.windows.net"),
                    serviceProvider.GetRequiredService<TokenCredential>());
        });
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<CosmosOptions>>()
                .Value;
            var clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway
            };

            if (options.DisableServerCertificateValidation)
            {
                clientOptions.HttpClientFactory = () =>
                    new HttpClient(
                        new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        });
            }

            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return new CosmosClient(options.ConnectionString, clientOptions);
            }

            if (string.IsNullOrWhiteSpace(options.Endpoint))
            {
                throw new InvalidOperationException(
                    "Configure Cosmos:ConnectionString locally or Cosmos:Endpoint in Azure.");
            }

            return new CosmosClient(
                options.Endpoint,
                new DefaultAzureCredential(),
                clientOptions);
        });

        services
            .AddOptions<SignalROptions>()
            .Bind(configuration.GetSection(SignalROptions.SectionName));

        services.AddSingleton<IRawEventArchive, BlobRawEventArchive>();
        services.AddSingleton<IProcessingQueue, StorageProcessingQueue>();
        services.AddSingleton<CosmosEventRepository>();
        services.AddSingleton<IEventRepository>(
            serviceProvider => serviceProvider.GetRequiredService<CosmosEventRepository>());
        services.AddSingleton<IObservationHistory>(
            serviceProvider => serviceProvider.GetRequiredService<CosmosEventRepository>());
        services.AddSingleton<IAlertDetector, AlertDetector>();
        services.AddSingleton<IAlertRepository, TableAlertRepository>();
        services.AddSingleton<IAuditRepository, TableAuditRepository>();
        services.AddSingleton<INotificationSender, LoggingNotificationSender>();
        services.AddSingleton<IRawEventIngestor, RawEventIngestor>();
        services.AddSingleton<IEventProcessor, EventProcessor>();

        services.AddSingleton<IRealtimeNotifier>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<SignalROptions>>()
                .Value;

            if (!string.IsNullOrWhiteSpace(options.RelayBaseUrl))
            {
                return new LocalRelayRealtimeNotifier(
                    options.RelayBaseUrl,
                    new HttpClient(),
                    serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LocalRelayRealtimeNotifier>>());
            }

            if (!string.IsNullOrWhiteSpace(options.Endpoint))
            {
                return new SignalRServiceNotifier(
                    options.Endpoint,
                    serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SignalRServiceNotifier>>());
            }

            return new LoggingRealtimeNotifier(
                serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LoggingRealtimeNotifier>>());
        });

        return services;
    }

    private static string RequireAccountName(string? accountName) =>
        !string.IsNullOrWhiteSpace(accountName)
            ? accountName
            : throw new InvalidOperationException(
                "Configure ProcessingStorage:ConnectionString locally or "
                + "ProcessingStorage:AccountName in Azure.");
}
