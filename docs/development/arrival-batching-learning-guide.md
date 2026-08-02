# Arrival Batching Learning Guide

Arrival batching is a small architectural change with a large operational
effect: the platform still stores every prediction separately for API queries,
but transports and broadcasts one polling-cycle batch. This guide explains the
few ideas responsible for most of that behavior and uses the deployed August 2,
2026 evidence as its boundary.

## The 80/20 View

Five principles explain most of the implementation:

1. Batch at the boundary where observations share one cause: a polling cycle.
2. Preserve individual event identities inside the batch.
3. Persist before notifying clients.
4. Keep legacy contracts during a staged deployment.
5. Measure application broadcasts and Azure billing units separately.

## 1. One polling cycle owns one transport batch

`PollArrivalsAsync` in
`src/TflAnalytics.Application/Ingestion/IngestionPoller.cs` calls the TfL API
for each monitored station and builds individual
`EventEnvelope<ArrivalPredictionObserved>` values in memory. It publishes those
envelopes once, inside an outer
`EventEnvelope<ArrivalPredictionBatchObserved>`.

The batch contract lives in
`src/TflAnalytics.Contracts/Events/ArrivalPredictionBatchObserved.cs`. The
outer event has no station or line because it represents all monitored
stations. The inner envelopes retain their station and line metadata.

This changes the transport operation count without changing what one prediction
means:

```text
Before: one poll -> about 160 raw Cosmos writes -> about 160 archives/queue items
After:  one poll -> one raw Cosmos batch -> one archive/queue item
```

Transferable lesson: group work where it naturally enters the system, while the
items still share an obvious owner and timestamp.

## 2. A batch is not the same as losing event identity

Each inner arrival still receives a deterministic ID from `EventIdFactory`.
The batch receives its own deterministic ID derived from the sorted inner event
IDs. This supports two different deduplication questions:

- Have we already transported this polling result?
- Have we already persisted this individual prediction?

`EventProcessor.CreateArrivalBatchAsync` in
`src/TflAnalytics.Application/Processing/EventProcessor.cs` validates and
passes each inner envelope to `IEventRepository.CreateArrivalAsync`. The
existing Cosmos conflict behavior therefore remains the individual persistence
boundary.

Transferable lesson: batching should reduce coordination overhead, not erase
the identity needed for idempotency, partitioning, or later queries.

## 3. Cosmos persistence completes before SignalR notification

`ProcessQueuedEvent` in
`src/TflAnalytics.Processing.Functions/Functions/ProcessQueuedEvent.cs` receives
a `ProcessingResult` only after the processor has attempted every individual
Cosmos write. It then maps the persisted envelopes to one
`ArrivalsBatchUpdated` payload and calls
`BroadcastArrivalsBatchAsync`.

The API remains unchanged: arrival queries still read individual records from
the `live-events` container through `CosmosEventRepository`. The browser push
is a freshness signal carrying the latest values; it is not the source of truth.

The guarantee has an important boundary. Cosmos writes are individual, not one
cross-item transaction. A retry can encounter a mixture of existing and newly
created items. Deterministic IDs make that retry safe, but a future
alert-enabled design should test partial-batch failure and alert delivery
explicitly.

Transferable lesson: durable state should normally precede realtime
notification, and the retry behavior must be understood at the actual
transaction boundary.

## 4. Compatibility makes deployment order safe

The processor continues to accept legacy `ArrivalPredictionObserved` events
while also accepting `ArrivalPredictionBatchObserved`. The Angular
`SignalRService` continues to listen for `arrivalsUpdated` and additionally
listens for `arrivalsBatchUpdated`.

That compatibility enabled this deployment order:

```text
processing consumer
  -> Angular dashboard consumer
  -> ingestion batch producer
```

Old queue items could still be processed, an old producer could still publish,
and the producer changed only after both consumers understood the new contract.

Transferable lesson: for an event-contract migration, deploy tolerant consumers
before the new producer and remove the legacy path only after the old message
lifetime has expired.

## 5. One invocation is not necessarily one billed message

A captured scheduled poll at `2026-08-02T12:00:00.5371309Z` contained 172
arrivals for all five stations in one `arrivalsBatchUpdated` WebSocket frame.
The frame was 62,582 bytes.

Azure SignalR counts outbound messages in 2-KB units. The observed frame is
therefore approximately 31 billable units per connected client, rather than a
literal one. Compared with 172 separate small messages, that sample reduced the
estimated units by about 82%.

The number of connected clients still multiplies outbound usage. `Message
Count` and `Outbound Traffic` in the Azure SignalR metrics are therefore the
operational measures; the number of calls to `BroadcastArrivalsBatchAsync` is
only the application measure.

Transferable lesson: optimize and measure against the provider's billing unit,
not only the method-call count.

## Execution Flow

```text
PollArrivals timer (five minutes)
  -> TfL arrivals API for five stations
  -> individual deterministic arrival envelopes
  -> one ArrivalPredictionBatchObserved raw Cosmos document
  -> Cosmos change-feed trigger
  -> one gzip Blob archive under eventType=arrival-batch
  -> one processing queue message
  -> ProcessQueuedEvent
  -> individual live-events Cosmos writes
  -> one arrivalsBatchUpdated SignalR invocation
  -> Angular filters the batch for the selected station
```

## What the Tests Prove

`tests/TflAnalytics.UnitTests/IngestionPollerTests.cs` proves that a poll
publishes an `ArrivalPredictionBatchObserved` envelope instead of one outer
event per prediction. It also preserves the feature-flag test for disabled
arrival ingestion.

`tests/TflAnalytics.UnitTests/ProcessingTests.cs` proves that one archived batch
becomes multiple individual repository writes and that all processed envelopes
are returned for one later broadcast.

The tests do not currently prove:

- the exact SignalR target is invoked once by `ProcessQueuedEvent`;
- partial-batch failure and retry behavior;
- Azure payload metering;
- an end-to-end scheduled receipt without a controlled client.

The deployed WebSocket capture supplies runtime evidence for the last boundary,
but it does not replace focused automated notifier tests.

## Applying the Pattern to Line Status

Line status is a good next use of the same pattern, with one correction: it is
not on the arrival timer. `PollArrivals` uses `IngestionArrivalsSchedule` every
five minutes, while `PollLineStatus` uses `IngestionLineStatusSchedule` every
ten minutes.

The same pattern is now implemented in code for line status. One poll normally
produces about 11 observations, which are carried by:

```text
LineStatusBatchObserved
  -> individual LineStatusObserved inner envelopes
  -> individual line-status Cosmos persistence
  -> one lineStatusesBatchChanged browser notification
```

The same consumer-first deployment sequence must still be used before enabling
the batch producer in Azure. The likely saving
is smaller than arrivals because there are only about 11 items per ten-minute
poll, but it would still reduce raw Cosmos, Blob, queue, Function, and SignalR
operations. Measure the serialized batch first: disruption reasons can make a
line-status payload larger than expected.

The ingestion and processing tests now cover one outer batch and individual
persistence. Remaining focused coverage should prove:

1. exactly one batch notifier call;
2. the status page replacing all current statuses from one batch;
3. legacy `lineStatusChanged` compatibility during rollout;
4. a controlled Azure WebSocket receipt after deployment.

## Try It

Run the cheapest repository checks around the batching boundaries:

```bash
dotnet test tests/TflAnalytics.UnitTests/TflAnalytics.UnitTests.csproj \
  --no-restore --filter 'IngestionPollerTests|ProcessingTests' \
  -m:1 --disable-build-servers

cd web/tfl-analytics-dashboard
npm run build
```

Safe experiment: create a unit-test fixture with two arrival predictions. Before
running it, predict these results:

- publisher event count: one;
- outer payload arrival count: two;
- repository write count after processing: two.

Then create the equivalent two-line fixture for a proposed line-status batch.
Keep the production timer and Azure environment unchanged.

## Continuous-Learning Loop

1. Define the user-visible goal: refresh all current observations with fewer
   cloud operations.
2. Name the enabling concept: batch by polling-cycle ownership while preserving
   inner identity.
3. Implement the smallest useful behavior: one new outer contract and one
   consumer path.
4. Prove it at the cheapest meaningful boundary: publisher count, repository
   count, then one protocol-client receipt.
5. Explain failures: identify whether they came from contract compatibility,
   persistence, deployment order, payload size, or provider metering.
6. Record the transferable lesson: optimize the coordination boundary without
   weakening durable state or retry safety.
