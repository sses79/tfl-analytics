# Future Work Plan

> [Documentation index](../README.md)

Status reviewed on August 1, 2026 after the post-demo cost and repository
cleanup. This document tracks remaining work; completed delivery history remains
in [`Plan.md`](../../Plan.md).

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

**Status: local/deployment guard complete; scheduled execution remains open**

Completed:

- Current architecture and reduced-cost settings are documented.
- Historical migration documents are separated from current runbooks.
- The July 30 Azure resource state and cost baseline are recorded.
- The post-deployment record captures the empty line-status condition.
- `scripts/smoke-azure-event-flow.sh` fails when the API has fewer than 11
  lines, omits `waterloo-city`, or reports a missing/stale `lastEventUtc`.
- The post-deployment checklist requires the freshness smoke result.

Remaining:

- Run that check after deployments and on a low-frequency schedule.

Exit criteria:

- A stale or empty pipeline produces a failing automated check with a clear
  diagnostic message.
- The post-deployment checklist uses the automated freshness result as required
  evidence.

## Phase 4 — Choose The Next Product Phase

**Status: product choice is unblocked; low-frequency scheduling remains in Phase 3**

After reliability is restored, choose one:

1. Complete Phase 5 reliability evidence:
   - capture browser receipt of each enabled SignalR message type;
   - verify arrival and line-status latency targets;
   - add focused API query and SignalR publication coverage.
2. Begin Phase 6 authentication and authorization with Microsoft Entra ID.

Recommendation: complete the Phase 5 reliability evidence before beginning
authentication. Do not add new Azure services until the existing event path is
reliable, measurable, and protected by freshness checks.

## Delivery Order

```text
Restore line-status
  -> add freshness guard
  -> stop disabled timer executions
  -> complete Phase 5 evidence
  -> decide whether to start Phase 6
```
