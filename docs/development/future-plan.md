# Future Work Plan

> [Documentation index](../README.md)

Status reviewed on August 2, 2026 after arrival and line-status batching were
deployed. This document tracks remaining work; completed delivery history
remains in [`Plan.md`](../../Plan.md).

## Current Position

The reduced-cost Azure platform and public hosts are healthy. The live
line-status data path was restored and verified on August 1 after replacing
unresolved hierarchical trigger binding expressions with flat host settings,
restoring the eleventh line, and correcting SignalR configuration and RBAC:

- `GET /api/dashboard/summary` returns a current `lastEventUtc` and 11 monitored
  lines.
- `GET /api/lines/status` returns 11 current records.
- `ArchiveRawEvents` initializes without indexing errors and processes fresh
  raw events.
- A controlled pull reaches an Azure SignalR client as `lineStatusChanged`.
- `scripts/smoke-azure-event-flow.sh` fails on missing, stale, or incomplete
  line-status data without requiring paid telemetry.

Arrival ingestion is enabled on a five-minute schedule for five stations.
Alert detection remains intentionally disabled.
The first controlled arrival pull published 156 observations. Sending each one
separately can exceed SignalR Free F1's 20,000-message/day allowance. Arrival
batching is deployed: each five-minute poll publishes one raw
batch, persists its observations individually, and emits one
`arrivalsBatchUpdated` notification after persistence. A controlled Azure pull
on August 2 delivered 161 persisted observations in one live SignalR invocation.
The equivalent line-status batching path is deployed and retains the legacy
single-line contract for rollout compatibility. A controlled Azure pull on
August 2 delivered all 11 lines in one `lineStatusesBatchChanged` invocation.
Processed `line-status` and `live-events` observations retain 24 hours of
history; transient `raw-events` retains four hours and the Blob archive remains
the durable raw record.

The next product phase is a passenger-focused station departure board. Alerts
and authentication are not prerequisites and remain outside this phase.

## Phase 1 — Restore The Live Line-Status Path

**Status: complete — August 1, 2026**

Trace one scheduled or controlled line-status observation through:

```text
PollLineStatus
  -> Cosmos DB raw-events
  -> Cosmos change-feed trigger / leases
  -> raw Blob archive
  -> processing queue
  -> ProcessQueuedEvent
  -> Cosmos DB line-status
  -> API and dashboard
```

Completed evidence:

1. A controlled pull published all 11 configured line-status events, including
   `waterloo-city`.
2. The Cosmos change feed advanced through the configured `leases` container,
   demonstrated by `ArchiveRawEvents` producing a recent gzip Blob for
   `waterloo-city`.
3. Both `processing` and `processing-poison` peeked at zero after the controlled
   run; `ProcessQueuedEvent` persisted current records.
4. `/api/lines/status` returned 11 lines and dashboard summary advanced to
   `lastEventUtc=2026-08-01T07:55:09.1456644Z`.
5. A real `@microsoft/signalr` protocol client received `lineStatusChanged` for
   the controlled pull. The recovery added the missing processing endpoint and
   the least-privilege `SignalR REST API Owner` role.
6. `scripts/smoke-azure-event-flow.sh` passed with 11 lines and an event age of
   72 seconds against a 1,200-second maximum.
7. A browser kept `/status` and `/dashboard` open through the scheduled poll and
   received all 11 `lineStatusChanged` WebSocket invocation frames. The pages
   updated automatically without a reload; a captured Hammersmith & City frame
   carried `observedAtUtc=2026-08-01T19:50:00.4900023Z`.

Exit criteria:

- Eleven monitored lines are returned by the API.
- `lastEventUtc` advances after scheduled polls.
- A controlled line-status change reaches the browser.
- Queue and poison-queue health are verified.
- The failure cause and recovery evidence are recorded in the post-deployment
  verification document.

## Phase 2 — Finish Reduced-Cost Runtime Controls

**Status: active-feed configuration complete; paused-trigger optimization deferred**

Completed:

- Bicep parameters explicitly control arrivals, alerts, and both schedules.
- Development parameters independently control arrivals and alerts.
- The five-minute arrival and ten-minute line-status schedules are explicit.
- Retired SQL settings, code, package, local container, diagnostics, and
  provisioning module were removed.
- Live Azure has `Arrival__Enabled=true` and `Alerts__Enabled=false`.
- The manual pull endpoint preserves the same arrival feature flag.

Remaining if arrivals are paused again:

- Set `AzureWebJobs.PollArrivals.Disabled=true` from Bicep so the timer itself
  stops instead of executing and returning immediately.
- Add a focused configuration test for that disabled-trigger setting.

Exit criteria:

- Disabled feeds create no scheduled Function executions or TfL requests.
- Re-enabling a feed is an explicit Bicep parameter change with a documented
  verification procedure.

## Phase 3 — Automate Baseline Freshness Verification

**Status: complete in code — August 2, 2026; scheduled runs begin after merge**

Completed:

- Current architecture and reduced-cost settings are documented.
- Historical migration documents are separated from current runbooks.
- The July 30 Azure resource state and cost baseline are recorded.
- The post-deployment record captures the empty line-status condition.
- `scripts/smoke-azure-event-flow.sh` fails when the API has fewer than 11
  lines, omits `waterloo-city`, or reports a missing/stale `lastEventUtc`.
- The post-deployment checklist requires the freshness smoke result.
- `.github/workflows/azure-freshness.yml` runs the public freshness check hourly
  and supports manual execution without Azure credentials.
- The smoke script accepts `API_BASE_URL` for CI while retaining deployment-output
  discovery for authenticated operator runs.
- A live execution on August 2 passed with 11 lines, Waterloo & City present,
  and an event age of 385 seconds against the 1,200-second limit.

Exit criteria:

- A stale or empty pipeline produces a failing automated check with a clear
  diagnostic message.
- The post-deployment checklist uses the automated freshness result as required
  evidence.

## Phase 4 — Reliability Closeout Before Passenger Features

**Status: complete in code — August 2, 2026; merge/deployment pending**

Completed:

- Browser and protocol-client evidence exists for `arrivalsBatchUpdated` and
  `lineStatusesBatchChanged`.
- Arrival and line-status batches persist individual Cosmos records before one
  SignalR notification.
- Public API freshness, 11-line completeness, Waterloo & City presence, Function
  health, and disabled-alert behavior were verified after deployment.
- Arrival and line-status transport and SignalR operation counts were reduced by
  batching without adding an Azure service or changing a SKU.
- Focused tests prove each processed arrival and line-status batch calls its
  batch notifier exactly once and never also calls the legacy per-item notifier.
- The station-arrivals repository now selects only the newest station snapshot,
  removes duplicate train predictions, orders by ETA, and applies the requested
  count after deduplication.
- Arrival query tests cover stale observations, duplicate trains, preservation
  of distinct predictions, ordering, count limiting, and invalid counts.
- The full local solution builds without warnings; 33 unit tests and three
  integration tests pass. The credential-dependent Azure smoke-test project
  remains intentionally skipped in the default test run.

Exit criteria:

- A stale production data path fails automatically without paid monitoring.
- One batch produces exactly one application notification in automated tests.
- The station-arrivals API has a tested latest-record and deduplication rule.

The new query behavior and scheduled workflow must be merged, the API image
deployed, and the post-deployment checklist recorded before calling Phase 4
complete in Azure. No new infrastructure or SKU is required.

## Phase 5 — Passenger Station Departure Board

**Status: active — Phase 5A deployed; Phase 5B and 5C implemented locally August 3, 2026**

Implemented and deployed:

- TfL prediction persistence retains prediction ID, vehicle ID, destination
  NaPTAN ID, `towards`, and `currentLocation`.
- TfL route sequences are retrieved through the existing typed client and
  cached in-process for 24 hours.
- The departure-board API returns destination choices, direct-route
  recommendations, platform groups, ordered trains, freshness, and per-train
  destination suitability.
- Direct matching requires destination-after-origin on the same directional
  branch and rejects trains whose reported terminus occurs before the selected
  destination.
- The Angular arrivals page is now a mobile passenger departure board with
  origin and destination selection, route/platform advice, live local
  countdowns, platform boards, train-location text, suitability labels, and a
  clearly stated prediction-not-GPS limitation.
- SignalR arrival batches refresh the selected departure board through the API.
- Operators can enable one-minute arrival polling for up to ten minutes; durable
  expiry restores the five-minute default automatically.

Deployment evidence: Victoria to King's Cross produced the correct Victoria
line outbound recommendation, Northbound Platform 3, Walthamstow direction,
and five-stop sequence. The live board classified suitable and unsuitable
trains from a fresh persisted snapshot, and both dashboard hostnames serve the
passenger board bundle.

Remaining before Phase 5 completion:

- deploy Phase 5B and 5C and capture browser evidence against live TfL data;
- verify Journey Planner responses for a route requiring an interchange and for
  a step-free preference.

### Product goal

Replace the analytics-style arrivals table with a passenger decision flow:

```text
Where am I going?
  -> which direct line and direction serves it?
  -> which platform?
  -> which train should I board?
  -> when will it arrive?
```

The first release supports direct Underground journeys from the five monitored
origin stations. Interchanges and full journey planning are deferred.

### Passenger screen

The page should present:

1. Selected origin station and a clear live/stale indicator.
2. A searchable destination-station selector.
3. A recommendation card with line, direction, platform, terminus/towards, and
   number of stops.
4. Separate platform boards, with trains ordered by time to station.
5. Each train's terminus, current reported location, expected time, and a clear
   “stops at your destination” result.
6. A small ordered station strip showing origin, intermediate stops, destination,
   and discrete estimated train positions.

The technical per-row `Observed` column should become one page-level “Updated X
seconds ago” indicator. “At platform”, “Due”, and minutes-to-arrival should be
the strongest visual information.

### Data truth and limitations

The current `destinationName` is a train terminus, not a list of stations served.
For example, a Victoria-line train towards Walthamstow Central can serve King's
Cross even though King's Cross is not its destination name. Direct-route advice
must therefore combine live predictions with ordered TfL route sequences.

The TfL arrivals payload exposes fields the current contract does not retain:

- `destinationNaptanId`;
- `towards`;
- `currentLocation`;
- the prediction ID and vehicle ID needed for stable train rows.

TfL reports textual states such as “Approaching …” and “At Platform”, not
continuous GPS coordinates. Train movement must be shown as discrete or
interpolated prediction state and labelled “Estimated from TfL predictions”. It
must not imply exact physical tracking. Train identity should use a composite of
line, vehicle, direction, and terminus rather than vehicle ID alone.

### Direct-route matching

Cache ordered route branches and station IDs. A direct route is valid when the
selected destination occurs after the selected origin on the same directional
branch:

```text
originIndex = route.indexOf(originStationId)
destinationIndex = route.indexOf(destinationStationId)

direct = originIndex >= 0 && destinationIndex > originIndex
```

Then match suitable predictions by line, directional branch, destination stop,
and platform. Never infer whether a train serves a station from terminus text
alone.

### Proposed contracts and endpoints

Add slow-changing route topology:

```text
LineRouteSequence
  lineId
  direction
  branchId
  ordered stations: stationId, stationName, sequence
```

Extend passenger arrivals with:

```text
predictionId, vehicleId
stationId, stationName
lineId, lineName
destinationStationId, destinationName, towards
platformName, direction, currentLocation
expectedArrivalUtc, secondsToStation, observedAtUtc
servesSelectedDestination, stopsUntilDestination
```

Proposed API surface:

```http
GET /api/stations/{stationId}/departure-board
GET /api/stations/{stationId}/destinations
GET /api/lines/{lineId}/route-sequences
```

The departure-board endpoint accepts an optional `destinationStationId` and
returns grouped route recommendations and ordered trains.

### Freshness and cost design

The current five-minute persistence poll is suitable for analytics but too slow
for a passenger departure board. Target:

- cache route topology for 24 hours;
- refresh live station predictions through a server-side cache every 20–30
  seconds;
- update browser countdowns locally every second;
- make at most one upstream TfL request per station cache window, regardless of
  connected passengers;
- broadcast one batch per refreshed station snapshot if SignalR remains the
  delivery mechanism.

Do not expose the TfL key to browsers or let every browser call TfL directly.
Measure TfL request rate, Function/API executions, SignalR outbound bytes, and
cache hit rate before changing the production schedule. Prefer existing compute
and storage; do not add an Azure service unless measurements justify it.

### Time-limited demo polling boost

**Status: complete and deployed — August 2, 2026**

Keep the normal arrival persistence schedule at five minutes. For demonstrations,
allow an operator to enable one-minute arrival polling for a maximum of ten
minutes, after which the system automatically returns to the five-minute
default. This is an interim demonstration capability, not the production
freshness design for the passenger departure board.

Implement the boost as runtime scheduling policy rather than by temporarily
editing the Function App timer setting:

- let the arrival timer evaluate once per minute;
- outside an active boost, perform the TfL poll only on five-minute boundaries;
- store an explicit UTC expiry such as `arrivalDemoPollingUntilUtc` in a small,
  managed-identity-accessible control record;
- default safely to five-minute polling when the control record is absent,
  invalid, unavailable, or expired;
- restrict activation to an authenticated operator command or deployment
  operation; do not expose a public anonymous endpoint;
- cap each activation at ten minutes and keep alerts disabled;
- log activation, expiry, skipped timer evaluations, and boosted polls without
  recording secrets or high-cardinality prediction identifiers.

Arrival batching remains unchanged during the boost. At the currently observed
batch size, a ten-minute demonstration adds approximately eight polls, 40 TfL
requests, eight raw batches, 24 Function executions across the ingestion and
processing path, and about 272 SignalR billing units per continuously connected
client. This short burst is compatible with the existing SignalR Free F1
allowance; continuous one-minute full-batch broadcasting is not.

Demo-boost acceptance criteria:

- an operator can request a ten-minute boost without an Azure deployment or
  Function host restart;
- arrival observations update approximately once per minute during the boost;
- the mode returns automatically to five-minute polling even if the operator
  disconnects;
- an expired or unreadable control record cannot leave rapid polling enabled;
- telemetry records the requested expiry and proves the return to the default;
- a focused automated test covers normal boundaries, active boost, expiry,
  invalid state, and control-store failure.

### Delivery slices

#### 5A — Direct passenger board

- Add the time-limited demo polling boost and its operational control.
- Add and cache route sequences.
- Retain the missing TfL prediction fields.
- Add origin/destination direct-route matching.
- Group trains by line, direction, and platform; order by ETA.
- Display platform, terminus, “serves destination”, and freshness prominently.
- Deliver an accessible mobile layout.

#### 5B — Station sequence and train state

**Status: implemented locally — August 3, 2026; deployment pending**

- Add the ordered station strip and stops remaining.
- Map `currentLocation` to discrete station/approaching/at-platform states.
- Add disruption context without re-enabling the alert workflow.
- Clearly label estimated movement and stale data.

#### 5C — Journey planning

**Status: implemented locally — August 3, 2026; deployment pending**

- Add interchanges, alternatives, accessibility preferences, and
  disruption-aware routing through TfL's Journey API rather than building a
  complete routing engine in this repository.

The implementation proxies typed TfL Journey Planner results through the API,
supports least-time, least-walking, and least-interchange preferences plus a
step-free-to-platform option, surfaces per-leg disruption detail, and caches
identical requests for one minute to bound upstream traffic. Passengers can
search TfL stations beyond the direct-route list; station-search results are
cached for 24 hours.

### Success criteria for 5A

- Victoria to King's Cross recommends the Victoria line in the correct
  direction and shows the correct platform.
- Only trains whose route serves the selected destination are recommended.
- Trains are ordered by arrival time and update without a page reload.
- A passenger can identify line, direction, platform, and train on mobile
  without reading technical timestamps.
- Data older than the agreed threshold is visibly stale and is never presented
  as live.
- Deterministic tests cover branches, direction, terminus-before-destination,
  missing TfL fields, and duplicate predictions.

### Explicit non-goals

- Do not enable the existing alert functions.
- Do not start Entra ID authentication as part of the passenger board.
- Do not claim GPS-accurate train movement.
- Do not support interchange journeys in 5A.
- Do not add Azure services before measuring the existing platform.

## Delivery Order

```text
Schedule freshness guard
  -> add batch-notifier and latest-arrival API tests
  -> add the time-limited arrival demo polling boost
  -> build direct passenger departure board
  -> add estimated station-sequence view
  -> evaluate TfL Journey API for interchange journeys
```
