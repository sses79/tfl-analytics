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
| Date | August 4, 2026 |
| Git commit | PR #49 merged to `main` as `901eac842d383761692cf721cd656c714f6eb086` |
| Change | Phase 5D passenger-first journey results: normalized alternatives, accessible destination combobox, deterministic station search, consolidated disruptions, and review hardening |
| Provisioning state | `Succeeded` — Container App revision `ca-tfl-api-dev-nhkpyupi--0000018`; Static Web App production environment `Ready` |
| Scope | Deployed the immutable merged API image and Angular production bundle only; Function packages and infrastructure were unchanged; `Alerts__Enabled=false` retained |
| Cost impact | No service, SKU, throughput, retention, or capacity changed. Existing one-minute journey caching and bounded 24-hour station-search caching remain; expected cost impact is negligible |

Latest verification evidence:

- PR #49 passed backend, dashboard, infrastructure, dependency, and secret
  checks. GHCR workflow run `30908344280` published immutable API image
  `901eac842d383761692cf721cd656c714f6eb086`; Container App revision
  `ca-tfl-api-dev-nhkpyupi--0000018` runs that image with provisioning
  `Succeeded`.
- Bicep compilation, ARM validation, and the full resource-group `what-if`
  succeeded. The preview proposed no resource creation, deletion, SKU,
  throughput, retention, or capacity change, but repeated known Azure-generated
  Function, Storage, RBAC, observability-tag, and Static Web App metadata drift.
  The rollout therefore used the documented scoped API and dashboard deployment
  path instead of applying unrelated ARM changes.
- The Static Web App production environment reached `Ready`; `demo.ti5g.com`
  serves arrivals bundle `chunk-XYRFFYA4.js` with the Phase 5D alternatives UI.
  The API, ingestion Function, and processing Function health endpoints all
  returned healthy. `Alerts__Enabled=false` remains set.
- The live Victoria departure board was fresh at
  `2026-08-04T12:25:00.0299963Z`, with 60 destinations and nine platform
  groups. The standard event-flow smoke passed with all 11 lines, Waterloo &
  City, and event age 241 seconds against the 1,200-second limit.
- A live King's Cross-to-Barking journey request returned three direct
  Hammersmith & City services departing at 13:34, 13:44, and 13:54. These are
  visible timetable variants beyond the five-minute deduplication threshold,
  rather than identical same-departure responses. Automated fixtures verify
  exact duplicate removal, null-time handling, alternative accessibility
  wording, and empty-combobox keyboard safety.
- Exact `Barking` ranked before `Barking Riverside` and `Barkingside`, but TfL
  returned canonical hub ID `HUBBKG`; direct-destination data uses the
  Underground StopPoint ID. Canonical parent/child ID matching therefore
  remains a follow-up before the direct-search acceptance criterion is fully
  satisfied.
- Data-service and workload-RBAC smoke tests passed. Cosmos remains free-tier
  at 1,000 RU/s, `line-status` and `live-events` retain 24-hour TTLs, SignalR
  remains Free F1 with local authentication disabled, and Azure SQL remains
  absent. A stale seven-day assertion and summary in the data-service smoke
  script were corrected to the deployed 24-hour baseline.

- PR #47 and its Angular security follow-up passed backend, dashboard,
  infrastructure, dependency, and secret checks. GHCR workflow run
  `30853770873` published immutable API image
  `2ca20127e55e196d37b421d5422c0071939df13d`; revision
  `ca-tfl-api-dev-nhkpyupi--0000017` runs that image with provisioning
  `Succeeded`.
- Bicep compilation, ARM validation, and the full resource-group `what-if`
  succeeded. The preview proposed no new service or SKU but repeated known
  Azure-generated Function, observability, and Static Web App drift. The rollout
  therefore used scoped API and dashboard deployment instead of applying
  unrelated ARM changes.
- The Static Web App production environment reached `Ready`; `demo.ti5g.com`
  serves `main-PZPB7IDX.js` and arrivals chunk `chunk-3NXE3GCD.js`, containing
  the Journey Planner, step-free option, interchange alternatives, and estimated
  train UI.
- A live Victoria departure board was fresh with eight platform groups, one
  current disruption, and a train mapped to `betweenStations` near High Street
  Kensington. Station search returned Camden Town, and the step-free Victoria
  to London Bridge Journey Planner request returned three journeys; the first
  contained four legs.
- Both Function health endpoints and API health returned healthy. The standard
  event-flow smoke passed with all 11 lines, Waterloo & City, and event age 519
  seconds against the 1,200-second limit. `Alerts__Enabled=false` and the alerts
  API remained empty.
- Data-service and workload-RBAC smoke tests passed: Cosmos remains free-tier at
  1,000 RU/s, SignalR remains Free F1 with local authentication disabled, Azure
  SQL remains absent, and each workload retains its least-privilege managed
  identity roles. Diagnostic-setting checks remain intentionally not applicable
  while infrastructure observability is disabled.

- PR #46 passed backend, dashboard, infrastructure, dependency, and full-history
  secret checks. GHCR workflow run `30768753258` published immutable image
  `290b8c9b3656c606ee9a14aa578591dbadb86585`; Container App revision
  `ca-tfl-api-dev-nhkpyupi--0000016` runs that image with provisioning
  `Succeeded`.
- Root Bicep compilation and ARM validation passed. Azure returned an internal
  service error for the full root `what-if` (tracking ID
  `4f5a6860-6bb8-4eee-a91b-6ad766d6e207`). The targeted storage preview proposed
  exactly one create, `runtime-control`, with no deletion or SKU change, so the
  deployment used that narrow module plus scoped ingestion app-setting updates.
- Live settings report `IngestionArrivalsSchedule=0 * * * * *`, storage account
  `sttflnhkpyupi`, and control container `runtime-control`. The private container
  exists, and Azure indexed `EnableArrivalDemoPolling` plus
  `GetArrivalDemoPollingStatus` alongside the existing ingestion Functions.
- A protected activation at `2026-08-02T22:02:14.7605103Z` returned a fixed
  expiry of `22:12:14.7605103Z`. The passenger page visibly updated each minute;
  API evidence advanced on off-boundary minutes including `22:04`, `22:06`,
  `22:11`, and `22:12`.
- At `22:12:38Z`, protected status returned `enabled=false`, interval five, and
  reason `baseline-skip`. The latest snapshot stayed at `22:12` throughout
  minutes 13 and 14, then advanced at the normal `22:15` boundary. This proves
  both automatic expiry and restoration of the five-minute default without an
  operator action or host restart.
- Both Function health endpoints returned healthy. The final standard event-flow
  smoke passed with 11 lines, Waterloo & City, and event age 420 seconds against
  the 1,200-second limit. Alerts remained disabled.
- The targeted storage deployment became the newest successful ARM deployment
  but intentionally lacks application outputs. `load-azure-outputs.sh` now
  selects only a deployment containing storage, API, ingestion, and processing
  outputs, preventing future package scripts from choosing partial deployments.

- Bicep compilation, ARM validation, and full resource-group `what-if` passed.
  The preview created or deleted no resource and changed no SKU, but repeated
  Azure-generated Function, Static Web App, and observability metadata drift.
  The rollout therefore used the documented scoped code-deployment path rather
  than applying unrelated ARM changes.
- GHCR workflow run `30765494779` published immutable API image
  `436bac8e3238510e4d8842dbee4251eb0ae3569e`. Azure revision
  `ca-tfl-api-dev-nhkpyupi--0000015` reports `Succeeded` and runs that image.
- Both deployed Function health endpoints returned healthy after package
  deployment. The live event-flow smoke passed with 11 lines, Waterloo & City,
  `lastEventUtc=2026-08-02T20:40:00.5632324Z`, and event age 179 seconds.
- The live Victoria-to-King's Cross departure-board request returned a fresh
  snapshot at `2026-08-02T20:45:00.4240846Z`, 60 direct destinations, two route
  branches, ten platform groups, 20 trains with reported location text, two
  suitable trains, and 18 trains correctly marked unsuitable. Its first
  recommendation was Victoria line outbound, Northbound Platform 3, towards
  Walthamstow Central, with the correct five-stop sequence through Green Park,
  Oxford Circus, Warren Street, and Euston.
- The route-sequence endpoint returned two current Victoria branches from TfL.
  The Static Web App default hostname and `demo.ti5g.com` both serve
  `chunk-4WGSDX7X.js`, containing `Departure board`, `Board this train`, and the
  passenger flow explainer.
- Data-service and workload-RBAC smoke tests passed. Cosmos remains free-tier at
  1,000 RU/s, SignalR remains Free F1 with local authentication disabled, and
  no Azure SQL resource exists. Diagnostics remain unchanged.

- Required Bicep compilation and full resource-group `what-if` succeeded before
  the line-status batch rollout. It proposed no new service or SKU but repeated
  known metadata/platform drift, so no ARM deployment was applied.
- Processing, dashboard, then ingestion were deployed in consumer-first order.
  Both Function health endpoints were healthy and Azure indexed all expected
  Functions. The Static Web App production environment reached `Ready`; both
  its default hostname and `demo.ti5g.com` serve `main-3D4S4N6J.js` with shared
  bundle `chunk-5IC4ZMT3.js`, which contains legacy `lineStatusChanged` and new
  `lineStatusesBatchChanged` handlers.
- A real Azure SignalR protocol client was connected before a controlled pull.
  The pull returned 164 arrivals and 11 line statuses. The client received one
  `lineStatusesBatchChanged` invocation containing all 11 configured line IDs,
  including `waterloo-city`, at
  `observedAtUtc=2026-08-02T12:54:16.0861992Z`; its JSON payload was 2,925 bytes.
- The standard event-flow smoke passed with 11 lines, `waterloo-city`, the same
  `lastEventUtc`, and an event age of 130 seconds. The alerts API remained empty.

- Required Bicep compilation and full resource-group `what-if` succeeded before
  deployment. It proposed no new service or SKU, but reported existing
  platform-property and observability-tag drift; no ARM deployment was applied
  during this code-only rollout.
- The compatibility-safe rollout order was processing, dashboard, then
  ingestion. Both Function health endpoints returned healthy and Azure indexed
  all expected Functions. The Static Web App default hostname and
  `demo.ti5g.com` serve `main-EKSK4KY7.js` with shared bundle
  `chunk-U6INQ34H.js`, which contains both legacy `arrivalsUpdated` and new
  `arrivalsBatchUpdated` handlers.
- A real Azure SignalR protocol client was connected before a controlled pull.
  The pull returned 161 arrival observations and 11 line statuses; the client
  received exactly one `arrivalsBatchUpdated` invocation containing all 161
  arrivals, `observedAtUtc=2026-08-02T09:17:02.971668Z`, and a 58,156-byte JSON
  payload. The Victoria API returned current records with that same observation
  timestamp, proving individual Cosmos persistence completed before broadcast.
- The standard event-flow smoke passed with 11 lines, `waterloo-city`,
  `lastEventUtc=2026-08-02T09:20:00.3846892Z`, and an event age of 22 seconds.
  The alerts API remained empty, confirming alert processing stayed disabled.
- Application Insights did not ingest the matching informational traces because
  processing intentionally filters `Information` logs. The deploying user also
  lacks Storage Queue Data Reader, so this verification does not claim a direct
  queue or poison-queue peek; successful API persistence and the post-persistence
  SignalR receipt provide end-to-end processing evidence.

- Targeted `what-if` proposed exactly the ingestion app-settings child update.
  Deployment `enable-poll-arrivals-20260802` succeeded at
  `2026-08-02T08:47:06.705831Z`; live settings report
  `Arrival__Enabled=True`, `Alerts__Enabled=false`, arrival schedule every five
  minutes, and line status every ten minutes.
- A controlled pull published 156 arrival observations. The Victoria arrivals
  API returned 20 current results with latest
  `observedAtUtc=2026-08-02T08:47:47.3882653Z`. Both processing and poison queues
  drained to zero, and the alerts API remained empty.

- Targeted `what-if` proposed exactly two changes: `line-status` and
  `live-events` `defaultTtl` from 604,800 to 86,400 seconds. Deployment
  `processed-events-ttl-24h-20260801` succeeded at
  `2026-08-01T21:00:00.732886Z`. Live reads confirmed both TTLs are 86,400,
  partition keys remain `/lineId` and `/stationId`, and conflict resolution
  remains `LastWriterWins`.
- The post-TTL event-flow smoke passed with 11 lines, `waterloo-city`, and
  `lastEventUtc=2026-08-01T21:00:00.6355649Z` at an event age of 59 seconds.

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
- After PR #39 merged, processing package deployment
  `cb0a24a7-f1aa-429b-90fb-beae887231bb` completed at approximately
  `2026-08-01T18:30Z`. The processing health endpoint and all expected Functions
  passed verification. A controlled client then received `lineStatusChanged`
  for `jubilee` at `2026-08-01T18:32:26.8707643Z`; the freshness guard passed
  with 11 lines and event age 132 seconds.
- `SignalR broadcast accepted...` is logged at `Information`, while the deployed
  processing `host.json` intentionally sets `Default=Warning` to control
  telemetry volume. The acceptance trace is therefore filtered from
  Application Insights; client receipt is the end-to-end success evidence. The
  historical `403` remains visible because failures are logged as warnings.
- Browser DevTools confirmed an active Azure SignalR WebSocket to hub
  `dashboardhub`. During the scheduled `19:50Z` poll, the browser received all
  11 `type: 1` `lineStatusChanged` invocation frames and `/status` plus
  `/dashboard` updated automatically. One captured Hammersmith & City message
  had `observedAtUtc=2026-08-01T19:50:00.4900023Z`. Access-token query values
  were excluded from the record.
- The post-merge dashboard deployment initially failed cleanly because the npm
  lockfile omitted two Linux WASM peer packages. After pinning those development
  dependencies and regenerating the lockfile, Docker `npm ci`, the Angular
  production build, and the Static Web Apps production deployment succeeded.
  `https://demo.ti5g.com/status` serves `main-VVFKKIPU.js`; its lazy status
  bundle contains the updated `Cosmos raw-events`, leases, Blob/queue, and
  ten-minute flow description.
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
