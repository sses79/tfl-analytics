# Future Work Plan

> [Documentation index](../README.md)

Status reviewed on July 31, 2026 after the post-demo cost and repository
cleanup. This document tracks remaining work; completed delivery history remains
in [`Plan.md`](../../Plan.md).

## Current Position

The reduced-cost Azure platform and public hosts are healthy. The live
line-status data path was restored on July 31 after replacing unresolved
hierarchical trigger binding expressions with flat host settings:

- `GET /api/dashboard/summary` returns a current `lastEventUtc` and 10 monitored
  lines.
- `GET /api/lines/status` returns 10 current records.
- `ArchiveRawEvents` initializes without indexing errors and processes fresh
  raw events.
- Arrival ingestion and alert detection are intentionally disabled.

Do not begin another product phase until line-status ingestion is reliable and
freshness is measured automatically.

## Phase 1 — Restore The Live Line-Status Path

**Status: core event flow restored; reliability evidence remains open**

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

Work:

1. Confirm `PollLineStatus` calls TfL successfully and publishes 11 raw events.
2. Inspect `raw-events` documents and the `leases` checkpoint state.
3. Confirm the flat `CosmosTrigger*Name` host settings resolve and
   `ArchiveRawEvents` produces recent raw Blob objects.
4. Verify `processing` drains and `processing-poison` remains empty.
5. Confirm `ProcessQueuedEvent` writes recent `line-status` documents.
6. Verify `/api/lines/status`, dashboard summary, and SignalR updates.
7. Add a low-cost event-flow heartbeat or equivalent freshness signal that does
   not require Log Analytics.

Exit criteria:

- Eleven monitored lines are returned by the API.
- `lastEventUtc` advances after scheduled polls.
- A controlled line-status change reaches the browser.
- Queue and poison-queue health are verified.
- The failure cause and recovery evidence are recorded in the post-deployment
  verification document.

## Phase 2 — Finish Reduced-Cost Runtime Controls

**Status: mostly complete**

Completed:

- Bicep parameters explicitly control arrivals, alerts, and both schedules.
- Development parameters set arrivals and alerts to disabled.
- The five-minute arrival and ten-minute line-status schedules are explicit.
- Retired SQL settings, code, package, local container, diagnostics, and
  provisioning module were removed.
- Live Azure has `Arrival__Enabled=false` and `Alerts__Enabled=false`.

Remaining:

- Disable the `PollArrivals` timer trigger itself while arrivals are paused,
  rather than invoking it every five minutes and returning immediately.
- Preserve the manual pull endpoint's existing feature-flag behavior.
- Add a focused infrastructure or configuration test for the disabled-trigger
  setting.

Exit criteria:

- Disabled feeds create no scheduled Function executions or TfL requests.
- Re-enabling a feed is an explicit Bicep parameter change with a documented
  verification procedure.

## Phase 3 — Automate Baseline Freshness Verification

**Status: documentation complete; automated guard open**

Completed:

- Current architecture and reduced-cost settings are documented.
- Historical migration documents are separated from current runbooks.
- The July 30 Azure resource state and cost baseline are recorded.
- The post-deployment record captures the empty line-status condition.

Remaining:

- Add a lightweight Azure smoke test that fails when:
  - `/api/lines/status` is empty;
  - `lastEventUtc` is missing; or
  - the latest observation exceeds a configurable age.
- Run that check after deployments and on a low-frequency schedule.
- Keep the check independent of paid Log Analytics/Application Insights.

Exit criteria:

- A stale or empty pipeline produces a failing automated check with a clear
  diagnostic message.
- The post-deployment checklist uses the automated freshness result as required
  evidence.

## Phase 4 — Choose The Next Product Phase

**Status: blocked on Phases 1 and 3**

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
