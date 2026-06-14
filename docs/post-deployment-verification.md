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
| Date | June 14, 2026 |
| Git commit | `57f1aa0` |
| ARM deployment | `phase4-alerts-20260614-202014` |
| Provisioning state | `Succeeded` |
| Scope | Phase 4 alert rules and Durable workflow |
| Cost impact | No SKU or capacity increase |
| Event Hubs tier | Basic, one throughput unit |
| Azure consumer group | `$Default` |

Latest verification evidence:

- Ingestion and processing Function health endpoints returned `healthy`.
- API and Static Web App health checks passed.
- All ten expected Functions were indexed.
- The processing identity became the Azure SQL Entra administrator.
- Controlled good-service and disruption events were archived in raw Blob
  Storage and processed through Cosmos DB.
- Durable instance
  `e73bf0ad0b0fbc4e42a25f75326d6cfd1cd9f2a6e9038148c383b88de4eca844`
  completed successfully.
- The matching `LineStatusDisruption` SQL alert completed before an
  `AlertRaised` Table Storage audit entity was written.
- The `processing`, `processing-poison`, and Durable work-item queues contained
  zero visible messages.
- Data-service, workload RBAC, and diagnostic-setting smoke tests passed.
- The top-level Azure what-if returned an Azure control-plane
  `InternalServerError`; full ARM validation and the focused SQL what-if passed
  before deployment.

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
  -> Azure SQL dbo.Alerts
  -> Table Storage audit
  -> mock notification log
```

The Azure Basic Event Hubs namespace uses the built-in `$Default` consumer
group. The dedicated `processing` consumer group is local-emulator-only.

## Function Executions

In the Azure portal:

1. Open `func-tfl-analytics-ingestion-dev-nhkpyupi`.
2. Open **Functions > PollArrivals > Monitor** and confirm successful executions
   approximately every 30 seconds.
3. Check `PollLineStatus` for successful executions approximately every two
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
2. Query `dbo.Alerts` in Azure SQL and confirm exactly one row exists for the
   source event.
3. Open storage account `sttflnhkpyupi` > **Storage browser > Tables > audit**
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
- SQL contains the alert and Table Storage contains its audit record.
- Processing queue returns to zero.
- Poison queue is empty.
- No unexplained Function or Application Insights errors remain.
- The deployment record at the top of this file is updated.
