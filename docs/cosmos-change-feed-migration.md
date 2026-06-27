# Proposal: drop Event Hubs, use the Cosmos DB change feed

**Status:** deployed and verified (2026-06-27) — code, local Compose, and Bicep
have been migrated; Azure now uses the Cosmos DB change feed, and the old Event
Hubs namespace has been deleted.
**Goal:** remove the only no-free-tier resource in the stack (Event Hubs Basic,
~£0.27/day / ~£8/month) and cut the Storage-transaction churn it causes, while
**keeping a real, demonstrable event/data flow**. Targets a run-rate well under
£1/day on mostly free-tier resources.

See `docs/event-hubs-usage.md` for rollback/history context on the retired Event
Hubs transport.

## Plan of record (decisions & scope)

This migration is one part of a broader minimal-cost demo redesign agreed on
2026-06-25. Context: the real run-rate was ~£1.88/day (Jun 24) — Functions
(processing) + Storage **transaction** churn + Event Hubs — not the ~£0.11/day a
stale cost snapshot had suggested.

**Resource verdict:**

- **Free-tier, keep as-is:** Cosmos DB (Free Tier, 1000 RU/s, 25 GB), SignalR
  Free_F1, Static Web App Free, Key Vault (negligible).
- **Paid drivers (Jun 24):** Functions-processing £0.67, Storage-transactions
  £0.63, **Event Hubs Basic £0.27 (the only resource with no free tier)**,
  Container Registry £0.124, Container App £0.12, Functions-ingestion £0.06.

**Decisions made:**

- **Event transport → Cosmos change feed** (this document). Dropping Event Hubs
  removes the only no-free-tier resource and trims the Storage churn it causes.
- **API hosting → keep Container App + ACR, optimise only.** Do **not** migrate
  the API onto Functions/SWA. Action is limited to verifying `minReplicas=0`
  (confirmed) and that nothing pins the app warm. (Decided against the larger
  ~£7/mo saving to avoid the porting risk.)
- **Arrival ingestion stays paused** (`Arrival__Enabled=false`, PR #23) — only the
  line-status pipeline runs, which already cut the two biggest drivers.

**Sequencing:** this transport swap is the main structural change; the Container
App optimisation is a cheap side check. Together with the arrival pause already in
effect, the target is a run-rate **< £1/day** on mostly free-tier resources.

## Why the change feed fits

- **Cosmos is already free.** The account runs on **Free Tier** with a
  **database-level shared throughput of 1000 RU/s** (`infra/bicep/modules/cosmos.bicep`).
  The change feed is read out of that same throughput we already pay £0 for — net
  new cost ≈ £0.
- **It removes a double cost.** Deleting Event Hubs removes both the £8/month
  Basic namespace **and** the Event Hubs checkpoint transactions on the
  `AzureWebJobsStorage` account (part of the Storage churn driver).
- **It is still a streaming primitive.** The change feed is a persistent, ordered
  log of inserts/updates per container. The demo story stays intact — and arguably
  becomes more tangible: *raw event lands in Cosmos → change-feed processor reacts
  in near-real-time → curated doc + SignalR push to the live dashboard.*
- **The swap is contained.** The transport already sits behind the
  `IEventPublisher` (produce) and `IRawEventIngestor` (consume) abstractions, so
  most of the pipeline is untouched.

## Architecture: before vs after

**Before (Event Hubs):**
```
Timer poll → EventHubsEventPublisher → Event Hub "tfl-events"
          → ArchiveEventHubEvents [EventHubTrigger] → IRawEventIngestor
          → Blob → queue → Cosmos (live-events / line-status) → SignalR
```

**After (Cosmos change feed):**
```
Timer poll → CosmosRawEventPublisher → Cosmos container "raw-events"
          → ArchiveRawEvents [CosmosDBTrigger] → IRawEventIngestor
          → Blob → queue → Cosmos (live-events / line-status) → SignalR
                                   ▲
                          "leases" container tracks change-feed position
```

Everything downstream of `IRawEventIngestor.ArchiveAndQueueAsync` is unchanged.

## What changes

### 1. Infra — add containers (`infra/bicep/modules/cosmos.bicep`)

Add two containers to the existing shared-throughput database (no dedicated RU,
so they draw from the free 1000 RU/s):

- **`raw-events`** — the new transport container. Suggested partition key
  `/partitionKey` set to `stationId ?? lineId` (mirrors the current Event Hub
  partition key) so per-entity ordering is preserved. Give it a short TTL
  (e.g. `defaultTtl` of a few hours) — like the old 1-day Event Hub retention,
  this is a transient transport, not durable history (history already lives in
  `live-events` / `line-status` / Blob).
- **`leases`** — the change-feed processor's bookkeeping container (partition key
  `/id`). The Functions Cosmos trigger can auto-create this, but declaring it in
  Bicep keeps infra explicit and avoids a metadata-write at runtime (note
  `disableKeyBasedMetadataWriteAccess: true` is set on the account).

### 2. Producer — replace the publisher implementation

- Add `CosmosRawEventPublisher : IEventPublisher`
  (`src/TflAnalytics.Infrastructure/Messaging/`) that does an `UpsertItemAsync`
  of the `EventEnvelope<TPayload>` into `raw-events`. Carry the same metadata that
  is currently on Event Hub application properties (`eventType`, `schemaVersion`,
  `stationId`, `lineId`) as **document fields** so consumers/queries can filter
  without unpacking the payload. Set the document `id` from `EventId` (idempotent
  upsert) and `partitionKey` from `stationId ?? lineId`.
- In `DependencyInjection.AddInfrastructure`
  (`src/TflAnalytics.Infrastructure/DependencyInjection.cs`):
  - Register `IEventPublisher → CosmosRawEventPublisher` instead of
    `EventHubsEventPublisher`.
  - Remove the `EventHubProducerClient` singleton and the `EventHubsOptions`
    binding. The ingestion app will need a `CosmosClient` (it currently only
    builds one in the processing DI path — reuse the same factory).

`IngestionPoller` and the `PollArrivals` / `PollLineStatus` timer triggers are
**unchanged** — they still call `IEventPublisher.PublishAsync`.

### 3. Consumer — swap the trigger

Replace `ArchiveEventHubEvents` with `ArchiveRawEvents`
(`src/TflAnalytics.Processing.Functions/Functions/`). The body is identical — loop
the batch and call `IRawEventIngestor.ArchiveAndQueueAsync` — only the trigger
attribute changes (isolated-worker form):

```csharp
[Function(nameof(ArchiveRawEvents))]
public async Task Run(
    [CosmosDBTrigger(
        databaseName: "%Cosmos__DatabaseName%",
        containerName: "%Cosmos__RawEventsContainerName%",
        Connection = "Cosmos",
        LeaseContainerName = "%Cosmos__LeasesContainerName%",
        CreateLeaseContainerIfNotExists = false)]
    IReadOnlyList<string> events,
    CancellationToken cancellationToken)
{
    foreach (var eventJson in events)
    {
        await _ingestor.ArchiveAndQueueAsync(eventJson, cancellationToken);
    }
}
```

### 4. Infra — delete Event Hubs

- Remove the `messaging` module call from `infra/bicep/main.bicep` and delete
  `infra/bicep/modules/messaging.bicep`.
- Remove the Event Hubs role assignments from
  `infra/bicep/modules/workload-rbac.bicep` (`ingestionEventHubSender`,
  `processingEventHubReceiver`) and the namespace/hub `param`s.
- Delete the `EventHubs__*`, `ProcessingEventHubName`, `ProcessingConsumerGroup`
  app settings from `infra/bicep/modules/compute.bicep` (both Function Apps).
- Delete the live Event Hub namespace resource (`evhns-...`) once the new path is
  verified.

### 5. Local development

- Drop the Event Hubs emulator (`infra/local/eventhubs-config.json`) and its
  `EventHubs__ConnectionString` entries from the `local.settings.example.json`
  files. The Cosmos emulator already used locally serves the change feed too, so
  the whole pipeline can run on a single emulator.

## Configuration changes

Remove: `EventHubs__FullyQualifiedNamespace`, `EventHubs__EventHubName`,
`EventHubs__credential`, `EventHubs__clientId`, `ProcessingEventHubName`,
`ProcessingConsumerGroup`.

Add (both apps already have `Cosmos__*`): `Cosmos__RawEventsContainerName=raw-events`,
`Cosmos__LeasesContainerName=leases`. The ingestion app additionally needs the
`Cosmos__Endpoint` / `Cosmos__credential` settings the processing app already has.

## RBAC

No new role types needed — both managed identities already hold **Cosmos DB Built-in
Data Contributor** (`sqlRoleAssignments` in `cosmos.bicep`), which covers writing
`raw-events` and reading the change feed + leases. The Event Hubs Data Sender/Receiver
assignments are simply deleted.

## Cost impact

| Item | Before | After |
|---|---|---|
| Event Hubs Basic | ~£0.27/day (~£8/mo) | **£0 (deleted)** |
| Event Hub checkpoint Storage txns | part of ~£0.63/day | removed |
| Cosmos throughput | £0 (free tier) | £0 — change feed reads come from the same shared 1000 RU/s |
| New `raw-events` writes / `leases` polling | — | within free 1000 RU/s |

Net: removes the only no-free-tier resource and trims Storage churn. Combined with
the already-applied arrival pause, this targets **< £1/day**.

## Trade-offs & risks

- **Single logical consumer, not multi-consumer pub/sub.** One change-feed
  processor (identified by the leases container + a lease prefix) consumes
  `raw-events`. Event Hubs' independent consumer groups are lost. Fine at demo
  volume; if multiple independent consumers are ever needed, add lease prefixes or
  bring Event Hubs back.
- **Continuous lease polling.** The change-feed processor polls the leases
  container on an interval, so it consumes a trickle of RU continuously. Covered by
  free tier, but it keeps the processing app's trigger active (similar to the
  always-listening Event Hub trigger today).
- **Throughput sharing.** `raw-events` draws from the shared 1000 RU/s. At current
  volume (line-status every 10 min; arrivals paused) this is comfortable, but a
  large backfill/replay could contend with `live-events` / `line-status`. Mitigate
  with the short TTL on `raw-events` and modest batch sizes.
- **Ordering granularity.** Change-feed ordering is guaranteed **within a logical
  partition**, matching the current per-`stationId`/`lineId` Event Hub ordering —
  provided the partition key is set as described.

## Cutover plan

1. Land containers in Bicep (`raw-events`, `leases`) — additive, safe to deploy
   while Event Hubs still runs.
2. Deploy the code change (publisher + trigger swap) behind the new config.
3. Verify end-to-end: events appear in `raw-events`, the trigger fires,
   `live-events` / `line-status` keep updating, dashboard still receives SignalR
   pushes.
4. Once green, remove the Event Hubs module/settings/RBAC and delete the `evhns-…`
   namespace.

> **⚠️ Before any full `az deployment group create`:** `infra/bicep/main.bicep`
> still unconditionally declares the `sql` module, even though the SQL server was
> deleted on 2026-06-23 (alerts now live in Table Storage). A full redeploy will
> **silently recreate** the SQL server. Gate the `sql` module behind a parameter
> (mirror the existing `enableObservability` pattern) **first** — this migration's
> Bicep changes are a likely moment to trip that landmine.

## Rollback

The Event Hubs path is restored by reverting the `IEventPublisher` registration
back to `EventHubsEventPublisher` and the trigger back to `[EventHubTrigger]`, then
redeploying the `messaging` module. Keep that commit isolated so a single revert
brings the old transport back if the change feed misbehaves under load.

---

# Implementation guide (for the implementing agent)

This section is a concrete, self-contained checklist. Paths/line numbers were
verified on 2026-06-26 (branch `dev`); re-grep before editing in case they drift.

## Exact file inventory

| File | Change |
|---|---|
| `infra/bicep/modules/cosmos.bicep` | Add `raw-events` + `leases` containers; add outputs for both names |
| `infra/bicep/main.bicep` | Pass `cosmosRawEventsContainerName`/`cosmosLeasesContainerName` into `compute` (near lines 97–98); add outputs (near 234–235) |
| `infra/bicep/modules/compute.bicep` | Add the two `param`s (near 19–20); add `Cosmos__RawEventsContainerName`/`Cosmos__LeasesContainerName` + the **trigger connection** settings to the **processing** app settings (near 461); delete the `EventHubs__*`/`ProcessingEventHubName`/`ProcessingConsumerGroup` settings (lines ~235–413) and the ingestion `EventHubs__*` settings |
| `infra/bicep/modules/messaging.bicep` | Delete the file |
| `infra/bicep/modules/workload-rbac.bicep` | Delete `ingestionEventHubSender` + `processingEventHubReceiver` and the `eventHubsNamespaceName`/`eventHubName` params, the two `existing` EH resources, and the two role-def `var`s |
| `src/TflAnalytics.Infrastructure/Messaging/CosmosRawEventPublisher.cs` | **New** — `IEventPublisher` impl writing to `raw-events` |
| `src/TflAnalytics.Infrastructure/Messaging/EventHubsEventPublisher.cs` | Delete (or keep for rollback; if kept, it's just unregistered) |
| `src/TflAnalytics.Infrastructure/Messaging/EventHubsOptions.cs` | Delete (after removing its binding) |
| `src/TflAnalytics.Infrastructure/Processing/CosmosOptions.cs` | Add `RawEventsContainerName = "raw-events"` and `LeasesContainerName = "leases"` |
| `src/TflAnalytics.Infrastructure/DependencyInjection.cs` | See "DI rewiring" below |
| `src/TflAnalytics.Processing.Functions/Functions/ArchiveEventHubEvents.cs` | Replace with `ArchiveRawEvents.cs` (`[CosmosDBTrigger]`) |
| `src/TflAnalytics.Processing.Functions/*.csproj` | Remove `...Extensions.EventHubs`; add `Microsoft.Azure.Functions.Worker.Extensions.CosmosDB` |
| `src/TflAnalytics.Infrastructure/*.csproj` | Remove `Azure.Messaging.EventHubs` once `EventHubsEventPublisher` is gone |
| `src/TflAnalytics.Processing.Functions/host.json` | Remove the `extensions.eventHubs` block; optionally add a `cosmosDB` block |
| `src/*/local.settings.example.json` | Swap EH keys for Cosmos-trigger keys (both projects) |
| `tests/TflAnalytics.IntegrationTests/LocalStackTests.cs` | Rewrite — it directly constructs `EventHubProducerClient`/`EventHubsEventPublisher` (lines ~88–92) |
| `infra/local/eventhubs-config.json` | Delete; check `docker-compose*.yml` / smoke-test scripts for references |

## Three gotchas that will break this if missed

1. **CosmosDB trigger uses a connection-name convention, not the app's `Cosmos__Endpoint`.**
   For identity-based connections the trigger reads `<Connection>__accountEndpoint`,
   `<Connection>__credential=managedidentity`, `<Connection>__clientId` — note
   `accountEndpoint`, **not** the `Cosmos__Endpoint` the app's own `CosmosOptions`
   uses. To avoid a name clash, use a **dedicated connection name** e.g.
   `Connection = "CosmosTrigger"` and set:
   - Azure (processing app, in `compute.bicep`):
     `CosmosTrigger__accountEndpoint = https://{cosmosAccountName}.documents.azure.com:443/`,
     `CosmosTrigger__credential = managedidentity`,
     `CosmosTrigger__clientId = processingIdentity.properties.clientId`.
   - Local (single connection-string value, like the existing `"EventHubs": "…"`):
     `"CosmosTrigger": "AccountEndpoint=https://localhost:8081/;AccountKey=COSMOS_EMULATOR_KEY;"`.

2. **The trigger delivers deserialized documents, not raw JSON strings.** The old
   `[EventHubTrigger]` gave `string[]` of bodies fed straight into
   `IRawEventIngestor.ArchiveAndQueueAsync(string eventJson)`. The Cosmos trigger
   binds to documents — bind to `IReadOnlyList<JsonElement>` (or a POCO) and
   **re-serialize each item back to JSON** before calling the ingestor. Critical:
   `RawEventIngestor` parses that JSON and calls
   `EventEnvelopeValidator.ReadMetadata`, which expects the **`EventEnvelope`
   shape** (camelCase `eventId`/`eventType`/`stationId`/`lineId`, serialized with
   `JsonSerializerDefaults.Web`). So the publisher must store the envelope fields at
   the **document root** (extra Cosmos system fields `_rid`/`_ts`/`id` are
   harmless). Don't nest the envelope under a wrapper property or `ReadMetadata`
   won't find the fields.

3. **Cosmos needs a lowercase `id`; the publisher must add it.** `EventEnvelope`
   serializes `EventId` as `eventId`, but Cosmos requires `id`. In
   `CosmosRawEventPublisher`, serialize the envelope to a `JsonObject`, then set
   `id = EventId` and `partitionKey = stationId ?? lineId`, and `UpsertItemAsync`
   with `new PartitionKey(stationId ?? lineId)`. Upsert (not create) keeps it
   idempotent if the same `EventId` is published twice.

## DI rewiring (`DependencyInjection.cs`)

Currently `AddInfrastructure` (called by **both** Functions apps) builds the
`EventHubProducerClient`; the `CosmosClient` is built only in
`AddProcessingInfrastructure` (processing-only). The publisher now needs Cosmos in
**both** apps, so:

- In `AddInfrastructure`: bind `CosmosOptions`, build the **`CosmosClient`**
  singleton here (move the existing factory from `AddProcessingInfrastructure`),
  and register `IEventPublisher → CosmosRawEventPublisher`. Remove the
  `EventHubsOptions` binding and the `EventHubProducerClient` registration.
- In `AddProcessingInfrastructure`: **remove** the now-duplicate `CosmosClient`
  factory (it resolves the one from `AddInfrastructure`). `CosmosEventRepository`
  is unaffected — it keeps resolving `CosmosClient`.
- Give `CosmosRawEventPublisher` a ctor of `(CosmosClient, CosmosOptions)` and
  call `client.GetContainer(options.DatabaseName, options.RawEventsContainerName)`
  internally — do **not** register a bare `Container` singleton (there would be
  ambiguity with the repository's containers).

## Bicep container snippet (`cosmos.bicep`)

Mirror the existing `liveEvents` resource. `raw-events` gets a **short TTL** so it
self-cleans (it's transient transport); `leases` has no TTL.

```bicep
resource rawEvents 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'raw-events'
  properties: {
    resource: {
      id: 'raw-events'
      defaultTtl: 14400        // 4h — transient transport, not durable history
      partitionKey: { kind: 'Hash', paths: [ '/partitionKey' ], version: 2 }
    }
  }
}
resource leases 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'leases'
  properties: {
    resource: { id: 'leases', partitionKey: { kind: 'Hash', paths: [ '/id' ], version: 2 } }
  }
}
```

Both draw from the database-level shared 1000 RU/s (no per-container throughput) —
keep it that way to stay on free tier. Add `output rawEventsContainerName` /
`output leasesContainerName` and thread them through `main.bicep` → `compute.bicep`
exactly like `cosmosLiveEventsContainerName` is today.

## Trigger function (`ArchiveRawEvents.cs`)

```csharp
[Function(nameof(ArchiveRawEvents))]
public async Task Run(
    [CosmosDBTrigger(
        databaseName: "%Cosmos__DatabaseName%",
        containerName: "%Cosmos__RawEventsContainerName%",
        Connection = "CosmosTrigger",
        LeaseContainerName = "%Cosmos__LeasesContainerName%",
        CreateLeaseContainerIfNotExists = false)]
    IReadOnlyList<JsonElement> documents,
    CancellationToken cancellationToken)
{
    foreach (var doc in documents)
    {
        var eventJson = doc.GetRawText();          // re-serialize the document
        await _ingestor.ArchiveAndQueueAsync(eventJson, cancellationToken);
    }
}
```

## Tests, build & verify

- **Unit tests:** the `EventHub` hits under `tests/.../obj/` and `bin/` are build
  artifacts — ignore them. The only **source** that references Event Hubs is
  `tests/TflAnalytics.IntegrationTests/LocalStackTests.cs`; rewrite its publish
  step to upsert into `raw-events` via `CosmosRawEventPublisher` and assert the
  document flows through to `live-events`/`line-status`.
- **Add a unit test** for `CosmosRawEventPublisher` asserting the document has
  `id == EventId`, the correct `partitionKey`, and that the envelope fields survive
  a round-trip through `EventEnvelopeValidator.ReadMetadata`.
- **Build/lint:** `dotnet build TflAnalytics.sln` and
  `az bicep build --file infra/bicep/main.bicep` (catches the dangling EH
  param/output references — fix every one the compiler flags).
- **Run tests:** `dotnet test`.
- **Local end-to-end:** start the Cosmos emulator, run both Functions apps, and
  confirm: a poll writes to `raw-events` → `ArchiveRawEvents` fires → blob + queue
  → `live-events`/`line-status` update → dashboard receives a SignalR push.
- **Azure verify (post-deploy):** check the processing Function App logs show
  `ArchiveRawEvents` invocations, and that `az cosmosdb sql container show` lists
  `raw-events` and `leases`. Then confirm `evhns-…` can be deleted with no traffic.

## Scope reminders

- This is **line-status only** right now (`Arrival__Enabled=false`) — expect a low,
  steady trickle of events, not high volume. Don't add per-container throughput.
- **Gate the `sql` Bicep module before any full `az deployment group create`** (see
  the ⚠️ in the cutover plan) — the EH-removal Bicep edits are exactly the kind of
  change that triggers a full redeploy.
- Update `docs/event-hubs-usage.md` (flip its status note to "replaced by the
  change feed") and `post-deployment-verification.md` once deployed.
