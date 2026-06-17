# Azure Resource Status & Running Cost

Snapshot taken 2026-06-17 against subscription `TfL Analytics Development` (`e3ea5ccc-661d-451b-8b3d-574300552e30`).

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
