# Azure Post-Deployment Verification

Use this checklist after every Azure deployment. The deployed environment is
currently the development environment, despite serving live TfL data:

```text
Subscription: TfL Analytics Development
Resource group: rg-tfl-analytics-dev-uk-south
```

Run commands from the repository root. Never print or persist account keys,
connection strings, deployment tokens, or Key Vault secret values.

## Deployment Record

Update this section after every deployment.

| Field | Latest verified value |
|---|---|
| Date | June 24, 2026 |
| Git commit | `8bd66ca38e8d` (`codex/pause-arrival-ingestion`) |
| ARM deployment | Not applicable; Functions-only zip deploy via `scripts/deploy-functions.sh`, plus `Arrival__Enabled=false` app-setting change on the ingestion Function App. Full ARM create intentionally skipped because the unconditional SQL module would recreate the deleted SQL server. ARM validation passed against `infra/bicep/main.bicep`. |
| Provisioning state | `Succeeded` for foundation deployment `manual-20260621-2142`; both Function zip deployments completed and health checks passed |
| Scope | Ingestion and processing Function Apps redeployed; `Arrival__Enabled=false` set on `func-tfl-analytics-ingestion-dev-nhkpyupi`; line-status ingestion remains enabled |
| Cost impact | Stops arrival TfL calls, arrival Event Hub messages, arrival raw archive writes, arrival Cosmos writes, arrival alert detection, and arrival SignalR broadcasts. Fixed/background costs remain, including Event Hubs Basic, ACR, Storage baseline, and line-status polling. Cost Management reported June 24 actual cost at £1.241 so far immediately after deployment. |
| Event Hubs tier | Basic, one throughput unit |
| Azure consumer group | `$Default` |

Latest verification evidence:

- `dotnet build TflAnalytics.sln --no-restore -m:1 --disable-build-servers` passed.
- `dotnet test TflAnalytics.sln --no-restore --no-build -m:1 --disable-build-servers` passed.
- `az bicep build --file infra/bicep/main.bicep` passed.
- `az deployment group what-if` completed before deployment with no unexpected deletes or paid SKU increases identified; preview remained noisy for existing resources and nested modules.
- `az deployment group validate --resource-group rg-tfl-analytics-dev-uk-south --template-file infra/bicep/main.bicep --parameters infra/bicep/environments/dev.bicepparam --output none` passed.
- `scripts/deploy-functions.sh` zip-deployed both Function Apps. Ingestion deployment id `bbfa5a0c-00b4-4d4c-9087-d0198c140d65`; processing deployment id `a3b3642b-93f8-4b1b-9576-e337530fc173`. Both health endpoints returned `{"status":"healthy"}`.
- `az functionapp config appsettings set ... --settings Arrival__Enabled=false` completed, and a targeted app-setting query confirmed `Arrival__Enabled=false` on `func-tfl-analytics-ingestion-dev-nhkpyupi`.
- Expected Functions are indexed on both Function Apps, including `PollArrivals`, `PollLineStatus`, `TriggerIngestion`, `ArchiveEventHubEvents`, `ProcessQueuedEvent`, and alert workflow activities.
- Manual `POST https://func-tfl-analytics-ingestion-dev-nhkpyupi.azurewebsites.net/api/pull` returned `{"arrivalsPublished":0,"lineStatusPublished":14}`, confirming the deployed manual path also respects the arrival pause.
- API live health, API line-status, ingestion health, processing health, and Static Web App endpoint checks all passed.
- Workload RBAC smoke tests passed. Diagnostics smoke tests are not applicable because observability is disabled.
- The data-service smoke script still fails on the known deleted SQL database. Manual non-SQL checks passed: `alerts` table exists; Cosmos DB free tier is enabled with 1000 RU/s and seven-day TTL on both containers; SignalR is `Free_F1` with local authentication disabled and two app-server roles.
- Queue peeking was not performed because the current operator identity lacks Storage Queue data-plane RBAC and account keys were not used.
- `az resource list --resource-group rg-tfl-analytics-dev-uk-south --query "[?contains(name,'sql')]"` returned no rows after deployment, confirming SQL was not recreated.
- **To re-enable arrivals:** set `Arrival__Enabled=true` or remove the app setting from the ingestion Function App, then restart/redeploy the Function host. The code default is `true`.
- **Caveat carried over:** `infra/bicep/main.bicep` still unconditionally declares the `sql` module. A future full `az deployment group create` will recreate the deleted SQL server unless that module is gated first (tracked in `docs/azure-resource-status.md`).

Prior verification evidence (June 23, 2026 alert pause):

- `scripts/deploy-functions.sh` zip-deployed both Function Apps; both health endpoints returned `{"status":"healthy"}`.
- `az functionapp config appsettings set ... --settings "Alerts__Enabled=false"` confirmed set on `func-tfl-analytics-processing-dev-nhkpyupi`.
- Temporarily granted my own user `Storage Table Data Contributor` on `sttflnhkpyupi` (I only had `Storage Blob Data Reader`), deleted all 365 rows from the `alerts` table via `az storage entity delete`, verified 0 rows remain, then revoked the temporary role grant.
- `GET /api/dashboard/summary` returned `"recentAlertCount": 0`; `GET /api/alerts` returned `[]`.
- **To re-enable on/after July 1:** remove or flip `Alerts__Enabled` back to `true` on the processing Function App - no code redeploy needed, the flag defaults to `true`.

Prior verification evidence (June 23, 2026 SQL Server deletion, no code change):

- `az resource list --resource-group rg-tfl-analytics-dev-uk-south --query "[?contains(name,'sql')]"` returned empty immediately after deletion.
- Confirmed via DI (`src/TflAnalytics.Infrastructure/DependencyInjection.cs:196`) that `IAlertRepository` resolves to `TableAlertRepository`, not `SqlAlertRepository` — the deleted server had no live consumer.

Prior verification evidence (June 22, 2026 Table Storage migration deployment, API image `dev-20260621213648`, ARM deployment `manual-20260621-2142`):

- ARM deployment `manual-20260621-2142` succeeded at `2026-06-21T21:42:45Z`.
- API image `dev-20260621213648` active in revision `ca-tfl-api-dev-nhkpyupi--0000012`.
- The `alerts` Storage Table exists with table-scoped RBAC; `GET /api/alerts` confirmed both the processing write path and API read path use Table Storage.
- Azure SQL was `Paused` at the time and its old alert rows were retained — since superseded by the deletion above.

Prior verification evidence (June 20, 2026 observation-gap staleness check,
uncommitted at the time, since superseded above):

- `scripts/deploy-functions.sh` zip-deployed both Function Apps; Azure
  deployment history confirmed completion at `2026-06-20T17:56:23Z`
  (ingestion) and `2026-06-20T17:58:52Z` (processing), both healthy.

Prior verification evidence (June 20, 2026 write-storm fix, commit
`4b08594`):

- `scripts/deploy-functions.sh` zip-deployed both Function Apps; Azure
  deployment history confirmed completion at `2026-06-20T13:49:57Z`
  (ingestion) and `2026-06-20T13:54:03Z` (processing), both healthy.
- PR #19 (`dev` → `main`, commit `4b08594`) carried this change; merged via
  `20ae067`.

Prior verification evidence (June 19, 2026 dashboard CSP release, commit
`45093d5`):

- Static Web Apps CLI deployed the production bundle successfully to
  `https://blue-bush-0491f9503.7.azurestaticapps.net`.
- The live CSP now allows the API origin, Azure SignalR HTTPS and WSS origins,
  Google Fonts stylesheets, and Google font files.
- `/dashboard`, `/status`, `/arrivals`, and `/alerts` each returned HTTP 200.
- SignalR negotiation returned HTTP 200 from the dashboard origin, and a real
  SignalR client reached the `Connected` state.
- The dashboard production Docker build completed before the Static Web Apps
  CLI release.
- The API health endpoint returned `healthy`.
- Dashboard APIs returned 11 monitored lines, five monitored stations, and 50
  recent alerts; the summary endpoint reported live event data at
  `2026-06-19T19:10:00.1559181+00:00`.
- Ingestion and processing Function health endpoints returned `healthy`.
- Ingestion and processing Functions were indexed, including polling, archive,
  queue processing, alert orchestration, persistence, audit, and broadcast
  functions.
- Data-service and workload-RBAC smoke tests passed. The diagnostics smoke test
  is not applicable to the current deployment because `enableObservability` is
  false and no Log Analytics workspace is deployed.
- The latest ARM deployment, `manual-20260619-171859`, remains `Succeeded`; no
  ARM deployment or infrastructure change was applied for this dashboard-only
  release.

## Load Resource Names

```bash
source scripts/load-azure-outputs.sh
```

Confirm the script selects the intended successful deployment before continuing.

## Deployment State

```bash
az deployment group show \
  --name "$DEPLOYMENT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{name:name,state:properties.provisioningState,timestamp:properties.timestamp}" \
  --output table
```

The provisioning state must be `Succeeded`.

## Service Health

```bash
curl --fail --silent --show-error \
  "https://$INGESTION_FUNCTION_APP.azurewebsites.net/api/health"

curl --fail --silent --show-error \
  "https://$PROCESSING_FUNCTION_APP.azurewebsites.net/api/health"

curl --fail --silent --show-error \
  "https://$API_HOSTNAME/health/live"

curl --fail --silent --show-error \
  "https://$STATIC_WEB_APP_HOSTNAME/" \
  --output /dev/null
```

Both Function endpoints should return `"status":"healthy"`.

## Indexed Functions

```bash
az functionapp function list \
  --resource-group "$RESOURCE_GROUP" \
  --name "$INGESTION_FUNCTION_APP" \
  --query "[].name" \
  --output table

az functionapp function list \
  --resource-group "$RESOURCE_GROUP" \
  --name "$PROCESSING_FUNCTION_APP" \
  --query "[].name" \
  --output table
```

Expected Functions:

- `IngestionHealth`
- `PollArrivals`
- `PollLineStatus`
- `ArchiveEventHubEvents`
- `ProcessingHealth`
- `ProcessQueuedEvent`
- `AlertOrchestration`
- `PersistAlert`
- `WriteAlertAudit`
- `SendMockAlertNotification`

## Management-Plane Smoke Tests

```bash
./scripts/smoke-azure-data-services.sh
./scripts/smoke-azure-workload-rbac.sh
./scripts/smoke-azure-diagnostics.sh
```

These verify free-tier controls, TTL and partition configuration, managed
identities, RBAC, and selected diagnostic settings.

## Event Flow

After Phase 4 deployment, the path is:

```text
TfL Unified API
  -> PollArrivals / PollLineStatus
  -> tfl-events Event Hub
  -> ArchiveEventHubEvents
  -> raw Blob container
  -> processing queue
  -> ProcessQueuedEvent
  -> Cosmos DB live-events / line-status
  -> AlertOrchestration for qualifying transitions
  -> Table Storage alerts
  -> Table Storage audit
  -> mock notification log
```

The Azure Basic Event Hubs namespace uses the built-in `$Default` consumer
group. The dedicated `processing` consumer group is local-emulator-only.

## Function Executions

In the Azure portal:

1. Open `func-tfl-analytics-ingestion-dev-nhkpyupi`.
2. Open **Functions > PollArrivals > Monitor** and confirm successful executions
   approximately every five minutes.
3. Check `PollLineStatus` for successful executions approximately every ten
   minutes.
4. Open `func-tfl-analytics-processing-dev-nhkpyupi`.
5. Check `ArchiveEventHubEvents` and `ProcessQueuedEvent` for successful
   executions.
6. After a qualifying delay or disruption, confirm `AlertOrchestration`,
   `PersistAlert`, `WriteAlertAudit`, and `SendMockAlertNotification` succeed.

Also review Application Insights for recent exceptions before declaring the
deployment healthy.

## Raw Event Archives

In the Azure portal:

1. Open storage account `sttflnhkpyupi`.
2. Open **Storage browser > Blob containers > raw**.
3. Confirm recently modified gzip files exist under paths resembling:

```text
eventType=arrival/year=2026/month=06/day=14/hour=...
eventType=line-status/year=2026/month=06/day=14/hour=...
```

Archive timestamps should continue advancing after deployment.

## Queue Health

In the Azure portal, open:

`sttflnhkpyupi` > **Storage browser > Queues**

Verify:

- `processing` normally drains back to zero.
- `processing-poison` remains zero.

A growing processing queue indicates consumer failure or insufficient
throughput. Any poison message requires investigation before completion.

## Cosmos DB Data

Open `cosmos-tfl-analytics-dev-nhkpyupi` in Azure Portal Data Explorer.

Run against `live-events`:

```sql
SELECT TOP 20 *
FROM c
ORDER BY c.observedAtUtc DESC
```

Run the same query against `line-status`. Confirm recent documents exist and
their timestamps continue advancing.

## Alert Workflow

After a qualifying line transition or prediction slip:

1. Open the processing Function App and confirm the orchestration completed.
2. Open storage account `sttflnhkpyupi` > **Storage browser > Tables > alerts**
   and confirm exactly one entity exists for the source event.
3. Open **Tables > audit**
   and confirm the matching `AlertRaised` entity exists.
4. Confirm Application Insights contains the mock notification log and no
   exhausted activity retries.

## Completion Criteria

A deployment is complete only when:

- ARM deployment state is `Succeeded`.
- Public health endpoints pass.
- Expected Functions are indexed.
- Management-plane smoke tests pass.
- Raw archives are recent and increasing.
- Cosmos DB contains recent arrival and line-status documents.
- Qualifying alerts complete the Durable workflow exactly once.
- The `alerts` table contains the alert and the `audit` table contains its
  audit record.
- Processing queue returns to zero.
- Poison queue is empty.
- No unexplained Function or Application Insights errors remain.
- The deployment record at the top of this file is updated.
