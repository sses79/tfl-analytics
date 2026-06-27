# How Event Hubs was used in this project

Azure Event Hubs was the **event transport** that decoupled ingestion from
processing. The ingestion Function App polls the TfL API on a timer, wraps each
observation in an envelope, and published it to an Event Hub. The processing
Function App was triggered by those events, archived the raw payload, and fanned
them into the downstream pipeline (Blob → queue → Cosmos → SignalR).

> **Status note (2026-06-27):** this path has been retired in Azure. Code and
> Bicep now use the **Cosmos DB change feed** transport described in
> `docs/cosmos-change-feed-migration.md`, and the Azure Event Hubs namespace has
> been deleted. Keep this document as rollback/history context.

## End-to-end flow

```
TfL API
  │  (HTTP poll on a timer)
  ▼
Ingestion Function App  ── PollArrivals / PollLineStatus (TimerTrigger)
  │  IIngestionPoller → IEventPublisher.PublishAsync(EventEnvelope<T>)
  ▼
EventHubsEventPublisher  →  Event Hub "tfl-events"   (Basic, 2 partitions, 1-day retention)
  │
  ▼  (EventHubTrigger, batched)
Processing Function App  ── ArchiveEventHubEvents
  │  IRawEventIngestor.ArchiveAndQueueAsync(eventJson)
  ▼
Blob (raw archive) → Storage queue → Cosmos DB → SignalR → dashboard
```

## The Event Hub resource

Declared in `infra/bicep/modules/messaging.bicep`, wired in
`infra/bicep/main.bicep` (the `messaging` module):

| Setting | Value | Notes |
|---|---|---|
| Namespace | `evhns-${projectName}-${environmentName}-${suffix}` | e.g. `evhns-tfl-analytics-dev-nhkpyupi` |
| SKU | **Basic**, capacity 1 | No free tier exists for Event Hubs — ~£0.27/day |
| Event Hub name | `tfl-events` | |
| Partition count | **2** | Partition key = `stationId ?? lineId` |
| Message retention | **1 day** | This is a live transport, not a long-term store; durable history lives in Cosmos/Blob |
| `disableLocalAuth` | `false` | Allows the local emulator / connection-string path; Azure uses Entra ID |

## The producer side (ingestion)

- **Triggers** — `PollArrivals` and `PollLineStatus`
  (`src/TflAnalytics.Ingestion.Functions/Functions/`) are `TimerTrigger`s on
  `%IngestionArrivalsSchedule%` (every 5 min) and `%IngestionLineStatusSchedule%`
  (every 10 min). They delegate to `IIngestionPoller`.
  - Note: arrival polling is currently disabled via `Arrival__Enabled=false`
    (PR #23), so only line-status events are produced right now.
- **Publishing** — `IngestionPoller` calls
  `IEventPublisher.PublishAsync(EventEnvelope<T>)`. The Event Hubs implementation
  is `EventHubsEventPublisher`
  (`src/TflAnalytics.Infrastructure/Messaging/EventHubsEventPublisher.cs`):
  - Serializes the `EventEnvelope<TPayload>`
    (`src/TflAnalytics.Contracts/Events/EventEnvelope.cs`) to JSON as the event body.
  - Sets `ContentType=application/json`, `MessageId=EventId`, and attaches
    `eventType`, `schemaVersion`, `stationId`, `lineId` as **application
    properties** (useful for routing/filtering without deserializing the body).
  - Sets `PartitionKey = stationId ?? lineId` so all events for a given
    station/line land on the same partition and stay ordered.
- **Client** — a singleton `EventHubProducerClient` built in
  `DependencyInjection.AddInfrastructure`
  (`src/TflAnalytics.Infrastructure/DependencyInjection.cs`), bound from
  `EventHubsOptions` (`EventHubs` config section).

## The consumer side (processing)

- **Trigger** — `ArchiveEventHubEvents`
  (`src/TflAnalytics.Processing.Functions/Functions/ArchiveEventHubEvents.cs`) uses
  `[EventHubTrigger("%ProcessingEventHubName%", ConsumerGroup="%ProcessingConsumerGroup%", Connection="EventHubs", IsBatched=true)]`.
  - `IsBatched = true` → it receives a `string[]` batch of event bodies per
    invocation.
  - For each event it calls `IRawEventIngestor.ArchiveAndQueueAsync(eventJson)`,
    which archives the raw JSON to Blob and queues it for the rest of the
    pipeline (Cosmos write, alert detection, SignalR push).
- **Checkpointing** — the Functions Event Hubs extension stores consumer
  checkpoints/leases in the processing app's `AzureWebJobsStorage` account. This
  is a notable contributor to that account's **transaction** cost.

## How Bicep wires the consumer trigger

There is no explicit "subscribe" call in application code. The subscription is
the combination of the Functions trigger binding, Function App settings, and
Azure RBAC:

1. `ArchiveEventHubEvents` declares the trigger:

   ```csharp
   [EventHubTrigger(
       "%ProcessingEventHubName%",
       ConsumerGroup = "%ProcessingConsumerGroup%",
       Connection = "EventHubs",
       IsBatched = true)]
   string[] events
   ```

2. `infra/bicep/modules/compute.bicep` sets the Processing Function App values
   consumed by that trigger:

   ```bicep
   {
     name: 'EventHubs__fullyQualifiedNamespace'
     value: '${eventHubsNamespaceName}.servicebus.windows.net'
   }
   {
     name: 'EventHubs__credential'
     value: 'managedidentity'
   }
   {
     name: 'EventHubs__clientId'
     value: processingIdentity.properties.clientId
   }
   {
     name: 'ProcessingEventHubName'
     value: eventHubName
   }
   {
     name: 'ProcessingConsumerGroup'
     value: '$Default'
   }
   ```

   `Connection = "EventHubs"` tells the Functions binding to read the
   `EventHubs__*` settings. `%ProcessingEventHubName%` and
   `%ProcessingConsumerGroup%` are expanded from app settings at runtime.

3. `infra/bicep/modules/workload-rbac.bicep` grants the Processing Function's
   managed identity permission to receive from the hub:

   ```bicep
   resource processingEventHubReceiver 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
     name: guid(eventHub.id, processingPrincipalId, eventHubsDataReceiverRoleDefinitionId)
     scope: eventHub
     properties: {
       principalId: processingPrincipalId
       principalType: 'ServicePrincipal'
       roleDefinitionId: eventHubsDataReceiverRoleDefinitionId
     }
   }
   ```

   `eventHubsDataReceiverRoleDefinitionId` is the built-in **Azure Event Hubs
   Data Receiver** role. The `scope: eventHub` keeps the permission limited to
   the `tfl-events` hub.

At runtime, the Functions host reads the trigger metadata, uses the configured
Processing managed identity to request an Entra token, Event Hubs checks the
Data Receiver RBAC assignment, and the trigger starts receiving batches from the
configured consumer group.

## Configuration keys

**Ingestion** (`EventHubs` section → `EventHubsOptions`):

| Key (Azure) | Local equivalent | Purpose |
|---|---|---|
| `EventHubs__FullyQualifiedNamespace` | `EventHubs__ConnectionString` | Namespace endpoint (Azure) vs emulator connection string (local) |
| `EventHubs__EventHubName` | same | Defaults to `tfl-events` |

**Processing** (binding-level settings consumed by the trigger):

| Key | Value | Purpose |
|---|---|---|
| `EventHubs__fullyQualifiedNamespace` + `EventHubs__credential=managedidentity` | — | Connection used by the `Connection="EventHubs"` trigger binding |
| `ProcessingEventHubName` | `tfl-events` | Hub the trigger listens on |
| `ProcessingConsumerGroup` | `$Default` (Azure) / `processing` (local) | Consumer group |

These are set on the Function Apps in `infra/bicep/modules/compute.bicep`
(see the `EventHubs__*`, `ProcessingEventHubName`, `ProcessingConsumerGroup`
app settings).

## Authentication

Azure uses **Entra ID / managed identity**, not connection strings. RBAC is
assigned in `infra/bicep/modules/workload-rbac.bicep`, scoped to the
`tfl-events` hub:

- Ingestion identity → **Azure Event Hubs Data Sender**
- Processing identity → **Azure Event Hubs Data Receiver**

The clients pick up `DefaultAzureCredential` when only a
`FullyQualifiedNamespace` (no connection string) is configured.

## Local development

For local runs the project uses the **Event Hubs emulator** instead of a real
namespace, configured in `infra/local/eventhubs-config.json`:

- Namespace `emulatorns1`, hub `tfl-events`, 2 partitions, consumer groups
  `archive` and `processing`.
- Locally the apps authenticate with a connection string
  (`EventHubs__ConnectionString=...UseDevelopmentEmulator=true...`) rather than
  managed identity — see the `local.settings.example.json` files in each
  Functions project.

## Why Event Hubs (and the trade-off)

- **Decoupling & buffering** — ingestion and processing scale and fail
  independently; a processing outage doesn't lose events within the 1-day
  retention window.
- **Ordering per entity** — partition-key by station/line keeps each entity's
  events ordered.
- **Cost trade-off** — Event Hubs Basic has **no free tier** (~£0.27/day,
  ~£8/month) and its checkpointing adds Storage-account transaction cost. At this
  project's low volume (a handful of stations/lines polled every 5–10 minutes),
  most of Event Hubs' real strengths (high-throughput partitioned fan-out,
  multiple independent consumer groups) are unused — which is what motivates the
  proposed move to the Cosmos change feed for a minimal-cost demo.
