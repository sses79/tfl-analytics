# Azure Resource Status & Running Cost

> [Documentation index](../README.md)

## Current state (2026-07-31)

Read-only verification against subscription `TfL Analytics Development`
confirmed the reduced-cost resource set:

- Kept: Storage, Key Vault, two Flex Consumption Function Apps, Container Apps
  Consumption, Static Web Apps Free, Cosmos DB Free Tier, SignalR Free F1, and
  three managed identities.
- Removed: Azure SQL, Event Hubs, and Azure Container Registry.
- Temporarily enabled for processing diagnostics: a workspace-based Application
  Insights component connected only to the processing Function App, backed by a
  PerGB2018 Log Analytics workspace with 30-day retention and a 0.1 GB/day cap.
- The unused SQL repository, client package, local container, and Bicep
  provisioning module were removed on July 31.
- API: public GHCR image, `minReplicas=0`, `maxReplicas=2`.
- Runtime flags: `Arrival__Enabled=false` and `Alerts__Enabled=false`.
- Schedules: arrival timer every five minutes (exits without a TfL request while
  disabled), line status every ten minutes.

July 1–30 Cost Management data returned approximately £2.17 for Storage,
£0.45 residual early-month Container Registry billing, £0.0002 for Key Vault,
and £0 for Cosmos DB, SignalR, and Functions. From July 20–29, Storage was
approximately £0.071–£0.080/day and is the main recurring cost.

The hosts and public endpoints were healthy on July 30, but the dashboard data
was empty and processing recorded no executions during the inspected 48-hour
window. Processing-only telemetry enabled on July 31 identified the cause:
`ArchiveRawEvents` fails indexing because `%Cosmos__DatabaseName%` does not
resolve in the Azure Functions host, after which the host disables the function.
The `raw-events` container continues receiving data while `leases` and
`line-status` remain empty. Host health alone does not prove pipeline health.

## Historical state (2026-06-23)

Snapshot taken 2026-06-23 against the same subscription. This section is
retained as cost-reduction history and does not describe the current resources.

### Snapshot

The `Monthly-Bill` budget (£100) shows £143.17 month-to-date, which reads as a
crisis but is almost entirely historical: Log Analytics ingestion (£64.59
MTD, disabled 2026-06-17, £0.00 every day since) and the `AlertDetector`
write storm driving Azure SQL (£33.12 MTD, fixed 2026-06-20, and alerts then
moved off SQL entirely onto Table Storage the same day). Today's actual
run-rate across the whole resource group is **£0.106/day**: Event Hubs
£0.045, Storage £0.034, Container Registry £0.016, Functions £0.010, SQL
£0.003, everything else £0.00.

Action taken 2026-06-23: deleted `sql-tfl-analytics-dev-nhkpyupi` (server +
`tfl-analytics` database) entirely. It had been fully superseded by
`TableAlertRepository` since `b079f29` (2026-06-20) — nothing in the code
referenced it anymore, it was just a paused, unused resource sitting on the
subscription. **Note:** `infra/bicep/main.bicep` previously declared the `sql`
module unconditionally, so a full `az deployment group create` would have
silently recreated the server. **Resolved 2026-06-27 (commit `0000e78`):** the
module is now gated behind `param enableSql bool = false`
(`module sql ... = if (enableSql)`), and consumers tolerate its absence
(`sql.?outputs.* ?? ''`, `enableSqlDiagnostics: enableSql`). Verified in the
2026-06-27 full deploy — `az resource list ... [?contains(name,'sql')]` returned
no rows afterward (see
`../deployment/post-deployment-verification.md`). A full redeploy
with the default params is now safe.

Resources currently free-tier or already scale-to-zero, left running as the
"essential function" baseline:
- Cosmos DB — Free Tier (1000 RU/s, 25 GB)
- SignalR — Free_F1
- Static Web App — Free
- Container App (API) — min replicas 0, scales to zero when idle
- Function Apps — Flex Consumption, no always-ready instances, polling
  already slowed (PR #15: arrivals every 5 min, line status every 10 min,
  plus an on-demand `/api/pull` trigger)
- Key Vault — negligible (~£0.0001/month)

Event Hubs (Basic, ~£0.045/day) is the one paid resource kept running
intentionally — no free tier exists for Event Hubs, and it's the ingestion
backbone.

## 2026-06-17 snapshot (historical, see above for current state)

## Resource groups

| Resource group | Region | Notes |
|---|---|---|
| `rg-tfl-analytics-dev-uk-south` | uksouth (SWA in westeurope, SQL in centralus) | Active application stack |
| `rg-tfl-analytics-dev-uks` | eastus | Orphaned — only contains a stray `Email` action group |

## Running state

| Service | Resource | Running state | Notes |
|---|---|---|---|
| Function App | `func-tfl-analytics-ingestion-dev-nhkpyupi` | **Running** | FlexConsumption (FC1) plan |
| Function App | `func-tfl-analytics-processing-dev-nhkpyupi` | **Running** | FlexConsumption (FC1) plan |
| Container App | `ca-tfl-api-dev-nhkpyupi` | **Running** | min replicas 0, max 2 — scales to zero when idle |
| Azure SQL DB | `tfl-analytics` (on `sql-tfl-analytics-dev-nhkpyupi`) | **Paused** (serverless GP_S_Gen5) | not billing compute, auto-resumes on connection |
| Cosmos DB | `cosmos-tfl-analytics-dev-nhkpyupi` | Active | Free Tier enabled, provisioned throughput (not serverless) |
| SignalR | `sigr-tfl-analytics-dev-nhkpyupi` | Active | Free_F1 tier — no cost |
| Event Hub Namespace | `evhns-tfl-analytics-dev-nhkpyupi` | Active | Basic SKU |
| Static Web App | `swa-tfl-analytics-dev-nhkpyupi` | Provisioned | westeurope |
| Container Registry | `acrtflnhkpyupi` | Active | Basic SKU |
| Storage Account | `sttflnhkpyupi` | Available | Standard_LRS |
| Key Vault | `kv-tfl-nhkpyupi` | Active | |
| Log Analytics Workspace | `log-tfl-analytics-dev-nhkpyupi` | Active | |
| App Insights | `appi-tfl-analytics-dev-nhkpyupi` | Active | |
| 3x Managed Identities | api / processing / ingestion | Active (auth objects, no runtime state) | |

## Running cost (month-to-date, 1–17 Jun 2026)

Pulled via `Microsoft.CostManagement/query` (ActualCost, grouped by ResourceId). Currency: GBP.

| Resource | MTD cost (GBP) |
|---|---|
| Log Analytics workspace (`log-tfl-analytics-dev-nhkpyupi`) | **£54.07** |
| Function App — processing | £7.41 |
| Storage account (`sttflnhkpyupi`) | £11.40 |
| Event Hub namespace | £1.45 |
| Function App — ingestion | £0.86 |
| Container Registry | £0.54 |
| Key Vault | £0.00005 |
| Cosmos DB | £0.00 |
| SignalR | £0.00 |
| Azure SQL DB | £0.00 (paused) |
| Stray action group (`rg-tfl-analytics-dev-uks`) | £0.00 |
| **Total MTD** | **≈ £75.72** |

No cost rows were returned for the Container App, Static Web App, App Insights, or the App Service plans backing the Function Apps — these have not accrued billable usage so far this period.

**Cost driver:** Log Analytics is ~71% of total spend this month, almost certainly from ingestion/retention volume rather than compute. Worth checking data retention settings and ingestion volume (verbose Function App logging, Datadog forwarding, etc.) if cost needs to come down.

At the current daily rate (~£4.45/day over 17 days), this projects to roughly **£133/month** if usage stays flat.
