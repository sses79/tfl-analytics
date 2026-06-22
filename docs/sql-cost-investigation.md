# Azure SQL cost investigation (2026-06-20)

## Summary

Azure SQL Database (`tfl-analytics` on `sql-tfl-analytics-dev-*`) has been
running well above its free-tier allowance every active day. The dominant
cost driver is **compute (vCore-seconds while online)**, not storage, and the
dominant *cause* of that compute usage is a **write storm** in alert
detection — not dashboard reads. `AlertDetector.DetectArrivalAsync` was
re-alerting (and re-inserting into SQL) on the same persistent arrival-delay
condition every single ~5-minute ingestion poll, instead of only on the
first occurrence.

The current branch removes Azure SQL from the active alert path. New alert
history is written to the `alerts` Storage Table, while the SQL implementation,
configuration, and paused Azure resource remain available for possible future
relational workloads.

## Cost breakdown by meter

Querying `Microsoft.CostManagement/query` (ActualCost, Daily) grouped by
`MeterSubcategory`/`Meter` and filtered to the SQL database's `ResourceId`:

| Component | Share of SQL cost |
|---|---|
| Compute (vCore overage, serverless) | ~99% |
| Storage | negligible (<1%) |

Daily SQL cost samples: Jun 17 £3.25, Jun 18 £7.56 (an ad-hoc admin SQL
session that day), Jun 19 £7.63 (flat vs Jun 18 despite a dashboard-read
cache deployed that day — see [cache fix note](#cache-fix-did-not-help)
below).

## Free tier mechanics

- Azure SQL's free database offer grants **100,000 vCore-seconds/month**
  (`useFreeLimit: true`, `freeLimitExhaustionBehavior: BillOverUsage` in
  `infra/bicep/modules/sql.bicep`).
- The quota resets at the start of each calendar month (00:00 UTC on the
  1st) — next reset **2026-07-01**.
- Tracked via Azure Monitor metrics on the SQL database:
  - `free_amount_consumed` / `free_amount_remaining` — capped at 100,000,
    stop reflecting usage once the quota is exhausted (not useful after
    exhaustion).
  - `app_cpu_billed` — actual daily vCore-seconds billed, including
    overage; the correct metric for usage-trend analysis.
- At current usage (~50,000-58,000 vCore-seconds on an active day), the
  monthly quota is exhausted within roughly **1-2 active days** of the
  reset. The free tier will **not** meaningfully cover usage for the rest
  of any month — overage billing kicks back in almost immediately.
- Serverless billing detail: the database bills per vCore-second while
  "online" and only auto-pauses after a full idle gap ≥ `autoPauseDelay`
  (60 minutes, the minimum allowed). Anything that touches SQL more often
  than every 60 minutes keeps it billing continuously.

## SQL usage at the time of the investigation

All alert SQL access went through `SqlAlertRepository`
(`src/TflAnalytics.Infrastructure/Alerts/SqlAlertRepository.cs`), which was
registered as the `IAlertRepository` implementation:

- `EnsureInitializedAsync` — lazy schema bootstrap, runs once per
  process/cold-start (gated by an in-memory `_initialized` flag).
- `CreateAsync` — INSERTs a new alert row. Called from
  `AlertActivities.PersistAlert` at the end of the Durable Functions
  orchestration that follows `AlertDetector.DetectArrivalAsync` /
  `DetectLineStatusAsync`.
- `GetRecentAlertsAsync` — SELECT for recent alerts. Two callers:
  - `AlertsController` (`/api/alerts`) — **uncached**, queried directly on
    every request.
  - `DashboardController.GetSummary` (`/api/dashboard/summary`) — wrapped
    in a 5-minute in-memory cache (deployed 2026-06-19, see below).

### Cache fix did not help

A 5-minute cache around the dashboard's alert-count read was deployed on
2026-06-19 on the theory that frequent dashboard polling was keeping SQL
"online". Full-day SQL cost on the deployment day (£7.63) came in flat
versus the day before (£7.56) — no measurable effect. This ruled out reads
as the dominant driver and motivated the write-side investigation below.

## Root cause: an undeduplicated write storm

Hitting the live `/api/alerts` endpoint showed the 50 most recent alerts
were *all* `ArrivalPredictionSlippage`, all created within a single
~3-second burst during one ingestion poll cycle, across nearly every line.

`AlertDetector.DetectArrivalAsync`
(`src/TflAnalytics.Application/Alerts/AlertDetector.cs:25-62`) fires
whenever a vehicle's predicted arrival shifts more than
`Alerts__ArrivalSlippageThresholdSeconds` (1200s) versus the **immediately
previous poll** — with no per-vehicle/line cooldown and no check for
whether the same delay condition was already alerted on last time. Because
the alert's identity hash includes `sourceEventId` (unique per
observation), a train whose prediction sits persistently past the
threshold gets a brand-new `CreateAsync` INSERT every single ~5-minute
arrivals poll, instead of being deduplicated as "the same ongoing delay".

This is catching TfL prediction noise/persistent delays, not one-off
disruptions, and is what's actually keeping SQL "online" almost
continuously — not dashboard or alerts-page reads (confirmed no
SQL-touching endpoint is polled on a timer from the dashboard frontend;
only `/health/live` runs on a 60s timer in `app.ts`).

By contrast, `AlertDetector.DetectLineStatusAsync`
(`AlertDetector.cs:64-91`) already does this correctly: it only alerts on
the *transition* from good service to disrupted (`previous.StatusSeverity
== GoodServiceSeverity && current < GoodServiceSeverity`), and stays silent
while a disruption persists. The arrival-slippage path lacked the
equivalent edge-detection.

## Fix

`AlertDetector.DetectArrivalAsync` now suppresses the alert if the
*previous* observation's own slippage (relative to its predecessor) was
already past the threshold — i.e. it only fires on the first poll where
slippage newly crosses the threshold, not on every subsequent poll while
the delay persists. This required threading one extra historical hop
through `IObservationHistory` / `CosmosEventRepository.GetPreviousArrivalAsync`
(`TOP 2` instead of `TOP 1`, same partition/query, negligible additional
Cosmos RU cost) so the detector can see the previous observation's own
prior value.
