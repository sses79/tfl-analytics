# Azure Post-Deployment Verification

> [Documentation index](../README.md)

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
| Date | August 1, 2026 |
| Git commit | Phase 1 completion on `dev`, based on `c8fae25` and recorded in PR #39 |
| Change | Complete line-status recovery: restore all 11 lines, add an API freshness guard, and restore processing-to-SignalR broadcasts |
| Provisioning state | `Succeeded` — ARM deployments `ingestion-waterloo-city-20260801`, `processing-signalr-settings-20260801`, and `processing-signalr-role-20260801`; Function hosts and public APIs healthy |
| Scope | Added `waterloo-city`; added the processing SignalR endpoint and `SignalR REST API Owner` RBAC; retained processing-only Application Insights, `Arrival__Enabled=false`, and `Alerts__Enabled=false` |
| Cost impact | £0 expected: app settings and RBAC only; no SKU or billable resource added. Diagnostic telemetry remains capped at 0.1 GB/day |

Latest verification evidence:

- All targeted Bicep `what-if` runs proposed only the intended app-setting or
  RBAC change; no billable resource or SKU changed. The full root `what-if`
  returned an Azure internal service error, so the deployments used compiled,
  narrowly scoped templates.
- A controlled pull published 11 line-status events. The API returned 11 current
  records including `waterloo-city`; dashboard summary advanced to
  `lastEventUtc=2026-08-01T07:55:09.1456644Z`.
- `scripts/smoke-azure-event-flow.sh` passed with `lineStatusCount=11`,
  `waterlooCityPresent=true`, and event age 72 seconds against the configured
  1,200-second limit.
- Blob archive evidence advanced to
  `eventType=line-status/year=2026/month=08/day=01/hour=07/lineId=waterloo-city/...json.gz`
  at `2026-08-01T07:42:19Z`. Non-destructive queue peeks returned zero for both
  `processing` and `processing-poison` after processing completed.
- The first broadcast attempt exposed two independent configuration defects:
  processing had no `SignalR__Endpoint`, then Azure returned HTTP 403 because
  its identity had the app-server role instead of REST-write permission. After
  the endpoint and `SignalR REST API Owner` role were deployed, a real
  `@microsoft/signalr` client received `lineStatusChanged` for `victoria` at
  `2026-08-01T07:55:09.1456644Z`.
- The obsolete processing `SignalR App Server` assignment was removed after the
  REST role propagated. A second deduplication-aware receipt test succeeded with
  only `SignalR REST API Owner`, receiving `circle` at
  `2026-08-01T08:03:19.0394037Z`.
- The solution build passed without warnings. All 29 runnable .NET tests passed;
  the one opt-in live Azure smoke test was skipped. Angular production build,
  Compose validation, Bicep compilation, shell syntax, and `git diff --check`
  passed.

Prior verification evidence (July 31, 2026 — Cosmos trigger recovery):

- The solution build passed with no warnings; 28 tests passed and one live Azure
  smoke test was intentionally skipped. Compose configuration passed using
  `.env.example`, and the root and targeted Bicep templates compiled.
- Azure returned an internal service error twice for the full root `what-if`.
  The replacement targeted `what-if` succeeded and proposed exactly the
  processing app-settings child resource, with 17 unrelated resources ignored.
- Deployment `processing-trigger-settings-20260731` added
  `CosmosTriggerDatabaseName=tfl-analytics`,
  `CosmosTriggerRawEventsContainerName=raw-events`, and
  `CosmosTriggerLeasesContainerName=leases`; package metadata references those
  same flat names.
- No `ArchiveRawEvents` indexing errors occurred after the corrected package
  started. A controlled manual pull published 10 configured line-status events.
- `GET /api/lines/status` returned 10 current records with
  `observedAtUtc=2026-07-31T16:41:42.6475188Z`. Dashboard summary returned
  `linesMonitored=10`, `linesDisrupted=6`, and the same `lastEventUtc`.
- The remaining 10-versus-11 line gap is configuration: `waterloo-city` is not
  currently present in the deployed line ID list. It is not a processing-path
  failure.
- Log Analytics `log-tfl-analytics-dev-nhkpyupi` reports `PerGB2018`, 30-day
  retention, and `dailyQuotaGb=0.1`.
- The first diagnostic hour contained 24 telemetry records totalling about
  0.0416 MB of billed data, so observed investigation cost is effectively zero.
- `APPLICATIONINSIGHTS_CONNECTION_STRING` exists on processing only; ingestion
  remains disconnected. No connection-string value was printed or persisted.
- The processing health endpoint returned `healthy`; end-to-end data checks now
  complement that host-only result.

Prior verification evidence (July 4, 2026 — ACR → GHCR cutover):

- `Publish API image` GitHub Actions workflow built `src/TflAnalytics.Api/Dockerfile` and pushed `ghcr.io/sses79/tfl-analytics-api:{latest,d4b7caeb…}` on push to `main`; run succeeded.
- The `tfl-analytics-api` GHCR package was marked **Public**; anonymous manifest pull of both the SHA tag and `latest` returned HTTP 200.
- `az containerapp update --image ghcr.io/sses79/tfl-analytics-api:d4b7caeb…` created revision `--0000013` (provisioning state `Succeeded`). Post-update: `/health/live` returned 200 (after cold start) and `/api/lines/status` returned 200 — confirming the ghcr image runs and reaches Cosmos / Table / Key Vault via the API managed identity.
- `az containerapp registry remove --server acrtflnhkpyupi.azurecr.io` succeeded; `properties.configuration.registries` is now empty. Endpoints re-verified 200.
- `az acr delete -n acrtflnhkpyupi --yes` succeeded; `az acr list` returned no rows, and `/health/live` + `/api/lines/status` re-verified 200 afterward. The ACR-pull role assignment was auto-removed with the registry (it was scoped to the ACR); the API managed identity remains for Key Vault / Table / SignalR.
- `az resource list ... [?contains(name,'acr') || contains(name,'sql')]` returned no rows.
- Bicep (`492ec1b`) already stripped the ACR resource, ACR-pull role,
  `registries` block, and `containerRegistry*` outputs. The inactive SQL
  provisioning module was subsequently removed in July 2026.

Prior verification evidence (June 27, 2026 — Cosmos change-feed migration; commit `0000e786e465`, ARM deployment `cosmos-change-feed-20260627-082434`, provisioning `Succeeded`; ingestion zip `aa038bc8-1017-4839-9f5b-7011a18c094f`, processing zip `e240105c-50ef-4ab0-8f65-63b1d94d3187`; migrated raw transport Event Hubs → Cosmos change feed, added `raw-events`/`leases`, deleted `evhns-tfl-analytics-dev-nhkpyupi`, kept `Arrival__Enabled=false`):

- `dotnet build TflAnalytics.sln --no-restore -m:1 --disable-build-servers` passed.
- `dotnet test TflAnalytics.sln --no-restore --no-build -m:1 --disable-build-servers` passed.
- `az bicep build --file infra/bicep/main.bicep` passed.
- `az deployment group what-if` completed before deployment with no unexpected paid SKU increases. The removed Event Hubs module appeared as `Ignore`, because the deployment mode is incremental and does not delete unmanaged resources.
- `az deployment group validate --resource-group rg-tfl-analytics-dev-uk-south --template-file infra/bicep/main.bicep --parameters infra/bicep/environments/dev.bicepparam --output none` passed.
- `az deployment group create --name cosmos-change-feed-20260627-082434 --resource-group rg-tfl-analytics-dev-uk-south --template-file infra/bicep/main.bicep --parameters infra/bicep/environments/dev.bicepparam` succeeded at `2026-06-27T07:26:24Z`.
- `scripts/deploy-functions.sh` zip-deployed both Function Apps. Ingestion deployment id `aa038bc8-1017-4839-9f5b-7011a18c094f`; processing deployment id `e240105c-50ef-4ab0-8f65-63b1d94d3187`. Both health endpoints returned `{"status":"healthy"}`.
- Cosmos DB container checks confirmed `raw-events` uses partition key `/partitionKey` with TTL `14400`, and `leases` uses partition key `/id`.
- App-setting checks confirmed ingestion has `Cosmos__RawEventsContainerName=raw-events` and `Arrival__Enabled=false`; processing has `CosmosTrigger__accountEndpoint`, `CosmosTrigger__credential=managedidentity`, `Cosmos__RawEventsContainerName=raw-events`, and `Cosmos__LeasesContainerName=leases`. No Event Hubs settings were returned by the targeted query.
- Expected Functions are indexed on both Function Apps, including `PollArrivals`, `PollLineStatus`, `TriggerIngestion`, `ArchiveRawEvents`, `ProcessQueuedEvent`, and alert workflow activities.
- `az eventhubs namespace delete --resource-group rg-tfl-analytics-dev-uk-south --name evhns-tfl-analytics-dev-nhkpyupi --no-wait true` completed; a follow-up `az eventhubs namespace show` returned `NamespaceNotFound`.
- Manual `POST https://func-tfl-analytics-ingestion-dev-nhkpyupi.azurewebsites.net/api/pull` returned `{"arrivalsPublished":0,"lineStatusPublished":11}`, confirming the deployed manual path still respects the arrival pause.
- Processing Function metrics showed post-deployment executions, including two executions in the `2026-06-27T07:35:00Z` five-minute bucket after the Cosmos change-feed package was deployed.
- API live health, API dashboard summary, API line-status, ingestion health, processing health, and Static Web App endpoint checks all passed. API data returned line statuses with `observedAtUtc=2026-06-27T07:20:00.1468213Z`; this timestamp is the TfL status observation time, not necessarily the Azure ingestion time.
- Azure Cost Management for June 27 showed partial-day costs of Container Registry £0.0052, Event Hubs £0.0112, Functions £0.00002, and Storage £0.0050 at verification time.
- `az resource list --resource-group rg-tfl-analytics-dev-uk-south --query "[?contains(name,'sql')]"` previously returned no rows after the gated SQL change, confirming SQL was not recreated by this deployment.
- **To re-enable arrivals:** set `Arrival__Enabled=true` or remove the app setting from the ingestion Function App, then restart/redeploy the Function host. The code default is `true`.
- **Caveat:** `az cosmosdb sql query` is not available in the local Azure CLI install, so raw document verification used container settings, Function metrics, and downstream API results instead of a direct SQL query against `raw-events`.

Prior verification evidence (June 23, 2026 alert pause):

- `scripts/deploy-functions.sh` zip-deployed both Function Apps; both health endpoints returned `{"status":"healthy"}`.
- `az functionapp config appsettings set ... --settings "Alerts__Enabled=false"` confirmed set on `func-tfl-analytics-processing-dev-nhkpyupi`.
- Temporarily granted my own user `Storage Table Data Contributor` on `sttflnhkpyupi` (I only had `Storage Blob Data Reader`), deleted all 365 rows from the `alerts` table via `az storage entity delete`, verified 0 rows remain, then revoked the temporary role grant.
- `GET /api/dashboard/summary` returned `"recentAlertCount": 0`; `GET /api/alerts` returned `[]`.
- **To re-enable on/after July 1:** remove or flip `Alerts__Enabled` back to `true` on the processing Function App - no code redeploy needed, the flag defaults to `true`.

Prior verification evidence (June 23, 2026 SQL Server deletion, no code change):

- `az resource list --resource-group rg-tfl-analytics-dev-uk-south --query "[?contains(name,'sql')]"` returned empty immediately after deletion.
- Confirmed via DI at the time that `IAlertRepository` resolved to
  `TableAlertRepository`, not the then-retained `SqlAlertRepository`; the
  inactive SQL implementation was subsequently removed in July 2026.

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
- `ArchiveRawEvents`
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
./scripts/smoke-azure-event-flow.sh
```

These verify free-tier controls, TTL and partition configuration, managed
identities, RBAC, and selected diagnostic settings.
The event-flow smoke additionally requires 11 current line statuses,
`waterloo-city`, and a fresh dashboard `lastEventUtc`. Override its default
20-minute limit with `MAX_EVENT_AGE_SECONDS` when necessary.

## Event Flow

After Phase 4 deployment, the path is:

```text
TfL Unified API
  -> PollArrivals / PollLineStatus
  -> Cosmos DB raw-events container
  -> ArchiveRawEvents
  -> raw Blob container
  -> processing queue
  -> ProcessQueuedEvent
  -> Cosmos DB live-events / line-status
  -> AlertOrchestration for qualifying transitions
  -> Table Storage alerts
  -> Table Storage audit
  -> mock notification log
```

The Cosmos DB trigger checkpoints through the `leases` container. Event Hubs is
no longer part of the Azure event path.

## Function Executions

In the Azure portal:

1. Open `func-tfl-analytics-ingestion-dev-nhkpyupi`.
2. Open **Functions > PollArrivals > Monitor** and confirm successful executions
   approximately every five minutes.
3. Check `PollLineStatus` for successful executions approximately every ten
   minutes.
4. Open `func-tfl-analytics-processing-dev-nhkpyupi`.
5. Check `ArchiveRawEvents` and `ProcessQueuedEvent` for successful
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
- `scripts/smoke-azure-event-flow.sh` passes.
- Raw archives are recent and increasing.
- Cosmos DB contains recent raw and processed line-status documents.
- Qualifying alerts complete the Durable workflow exactly once.
- The `alerts` table contains the alert and the `audit` table contains its
  audit record.
- Processing queue returns to zero.
- Poison queue is empty.
- No unexplained Function or Application Insights errors remain.
- The deployment record at the top of this file is updated.
