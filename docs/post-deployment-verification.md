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
| Date | June 22, 2026 |
| Git commit | Uncommitted Table Storage migration based on `fe1c271`; API image `dev-20260621213648` |
| ARM deployment | `manual-20260621-2142` |
| Provisioning state | `Succeeded` |
| Scope | Added `alerts` Storage Table, table-scoped API reader RBAC, API revision `ca-tfl-api-dev-nhkpyupi--0000012`, and ingestion/processing Function packages |
| Cost impact | Negligible Storage Table capacity/transactions; no new SKU; Azure SQL retained and verified paused |
| Event Hubs tier | Basic, one throughput unit |
| Azure consumer group | `$Default` |

Latest verification evidence:

- ARM deployment `manual-20260621-2142` succeeded at
  `2026-06-21T21:42:45Z` after Bicep compilation, what-if, and ARM validation.
- API image `dev-20260621213648` is active in ready revision
  `ca-tfl-api-dev-nhkpyupi--0000012`.
- `scripts/deploy-functions.sh` successfully deployed both Function Apps; both
  anonymous health endpoints returned `{"status":"healthy"}` and all expected
  ingestion, processing, Durable, audit, and broadcast Functions were indexed.
- The `alerts` Storage Table exists. The API identity has Storage Table Data
  Reader scoped to that table, while the processing identity retains its
  existing Table Data Contributor role.
- `GET /api/alerts` returned alerts detected after deployment, proving the
  processing write and API read paths both use Table Storage. The dashboard
  summary returned current Cosmos-backed state through `2026-06-22T07:30:00Z`.
- Azure Monitor reported aggregate `QueueMessageCount` maximum `0` for the
  latest one-hour sample. Today’s raw archive partition also contained recent
  arrival data.
- Data-service and workload-RBAC smoke tests passed. Diagnostics are not
  applicable because observability resources are disabled in this deployment.
- Azure SQL remained `Paused` after repeated alert and dashboard API queries;
  its old alert rows were deliberately retained to avoid waking the database.
- The live alert table already contains at least 50 recent alerts. Storage cost
  is controlled, but detector noise remains a separate follow-up concern.

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
