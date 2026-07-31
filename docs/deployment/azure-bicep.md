# Azure Bicep Deployment Guide

> [Documentation index](../README.md)

This guide covers validation, preview, deployment, output discovery, and smoke
testing for the current Phase 1 Azure foundation.

Run commands from the repository root.

For the complete application release sequence, including API image, Function
packages, dashboard content, smoke tests, and rollback, use the
[manual deployment runbook](./manual-deployment.md).

## Current Foundation

The Bicep deployment currently creates:

| Resource | Development configuration |
|---|---|
| ADLS Gen2 storage | StorageV2, Standard LRS, hierarchical namespace |
| Blob container | `raw` |
| Storage queues | `processing`, `processing-poison` |
| Storage tables | `alerts`, `audit` |
| Key Vault | Standard, RBAC authorization, seven-day soft delete |
| Log Analytics | PerGB2018, 30-day retention |
| Application Insights | Workspace-based, 30-day retention |

The Phase 1 compute foundation is deployed:

| Resource | Development configuration |
|---|---|
| Ingestion Functions | .NET 10 isolated, Flex Consumption, managed identity |
| Processing Functions | .NET 10 isolated, Flex Consumption, managed identity |
| Function host and deployment storage | Dedicated Blob containers plus identity-based Blob, Queue, and Table access |
| Angular hosting | Static Web Apps Free tier in West Europe |
| API hosting | Azure Container Apps Consumption, scale to zero, maximum two replicas |
| API image registry | Public GitHub Container Registry (`ghcr.io/sses79/tfl-analytics-api`); Container App pulls anonymously |
| Cosmos DB | Lifetime free tier, shared 1,000 RU/s, two seven-day TTL containers |
| Azure SQL | Removed — alerts now use Table Storage and no SQL provisioning module remains |
| Azure SignalR | Free F1, local key authentication disabled |

The reduced-cost development parameters are explicit in
`infra/bicep/environments/dev.bicepparam`: arrival ingestion and alert detection
are disabled, the arrival timer is scheduled every five minutes but exits
without calling TfL, and line status is scheduled every ten minutes. Retired
`AlertStorage__*` SQL settings are not deployed to either Function App or the
API Container App.

Processing-only Application Insights can be enabled independently with
`enableProcessingObservability=true`. The development environment currently
uses this mode with a 0.1 GB/day workspace ingestion cap. It does not connect
ingestion, the API, or platform diagnostic settings. Use
`infra/bicep/processing-observability.bicep` for a targeted monitoring change
when a full root `what-if` contains unrelated provider-default noise.

The Function hosts and their application packages are deployed. The Angular
line-status dashboard is deployed to the Static Web App and calls the Container
App API through an origin-restricted CORS policy.

Event transport uses the **Cosmos DB change feed**: ingestion writes raw events to
the `raw-events` container and the processing Function's Cosmos DB trigger consumes
them, tracking position in the `leases` container. Event Hubs — the prior transport
— was removed on 2026-06-27 (see
`../history/cosmos-change-feed-migration.md`).

Cosmos DB and SignalR are deployed in UK South. The Azure SQL server was deleted on
2026-06-23 after alerts moved to the `alerts` Storage Table. The SQL Bicep
module, local SQL Server container, SQL client dependency, and inactive
repository were removed in July 2026.

Linux App Service `P0v4` was evaluated at `$0.0913` per hour in UK South on
June 12, 2026, or approximately `$15.34` for seven continuous days. Although
within budget, Azure rejects the deployment because this subscription offer has
zero Microsoft.Web total regional VM quota. Container Apps Consumption avoids
that dedicated App Service quota and can scale the API to zero. The API image was
moved from Basic ACR (a flat ~`$0.1666`/day fixed fee) to public GitHub Container
Registry on July 4, 2026, which is free for this public repo — see
`../history/ghcr-image-migration.md`.

Default deployment settings:

```text
Subscription: TfL Analytics Development
Resource group: rg-tfl-analytics-dev-uk-south
Region: uksouth
Environment: dev
```

## Authenticate

Sign in and select the development subscription:

```bash
az login
az account set --subscription "TfL Analytics Development"
```

Verify the active context:

```bash
az account show \
  --query "{subscription:name, subscriptionId:id, tenantId:tenantId}" \
  --output table
```

Confirm the resource group and region:

```bash
az group show \
  --name rg-tfl-analytics-dev-uk-south \
  --query "{name:name, location:location, state:properties.provisioningState}" \
  --output table
```

The location should be `uksouth`.

## Validate Bicep

Check the installed Bicep version:

```bash
az bicep version
```

Compile the template:

```bash
az bicep build --file infra/bicep/main.bicep
```

The generated `infra/bicep/main.json` file is build output and should not be
committed.

## Preview Changes

Always run `what-if` before deployment:

```bash
az deployment group what-if \
  --name tfl-foundation-dev \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev.bicepparam
```

Review additions, modifications, replacements, and deletions before proceeding.
Pay particular attention to changes involving:

- Cosmos DB throughput or container TTL.
- Log Analytics and Application Insights retention or ingestion.
- Storage redundancy or access tier.
- Key Vault networking and retention.

These settings can affect cost, availability, or recovery behavior.

On June 13, 2026, `what-if` completed but marked the nested Container App
deployment as ignored because its dashboard-origin parameter contains a
resource reference that cannot be fully evaluated during preview. Review the
remaining changes, then use ARM deployment validation as the additional gate.
Do not skip compilation, `what-if`, or deployment validation.

## Deploy

Deploy only after reviewing the `what-if` result:

```bash
az deployment group create \
  --name tfl-foundation-dev \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev.bicepparam \
  --output table
```

Check deployment status:

```bash
az deployment group show \
  --name tfl-foundation-dev \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --query "{name:name, state:properties.provisioningState, timestamp:properties.timestamp}" \
  --output table
```

The provisioning state should be `Succeeded`.

## API Image Deployment

The API image is published to **public GitHub Container Registry**
(`ghcr.io/sses79/tfl-analytics-api`) by the `Publish API image` GitHub Actions
workflow on push to `main` (tags: `latest` + commit SHA). The Container App pulls
it anonymously — no registry credentials and no managed-identity pull role.

Deploy Bicep (or `az containerapp update`) with `apiImageTag` set to the published
SHA, after the image exists. To build out-of-band instead of via the workflow,
push to the same repo:

```bash
echo "$GITHUB_TOKEN" | docker login ghcr.io -u <github-user> --password-stdin

docker buildx build \
  --platform linux/amd64 \
  --file src/TflAnalytics.Api/Dockerfile \
  --tag "ghcr.io/sses79/tfl-analytics-api:$API_IMAGE_TAG" \
  --push .
```

## Function Package Deployment

Publish, package, deploy, and smoke-test both Function applications:

```bash
./scripts/deploy-functions.sh
```

The script:

1. Loads deployed resource names using `scripts/load-azure-outputs.sh`.
2. Publishes Release builds under the ignored `artifacts/functions` directory.
3. Creates one zip package per Function host.
4. Deploys the packages using Azure Functions zip deployment.
5. Verifies the anonymous health endpoints:

```text
https://func-tfl-analytics-ingestion-dev-nhkpyupi.azurewebsites.net/api/health
https://func-tfl-analytics-processing-dev-nhkpyupi.azurewebsites.net/api/health
```

The ingestion package contains arrival and line-status timer triggers plus its
health function. The processing package contains the Cosmos change-feed archive
trigger (`ArchiveRawEvents`), the Storage Queue processing trigger, and its health
function.

Phase 3 was deployed on June 14, 2026. The Azure smoke run verified raw Blob
archives and documents in both Cosmos DB containers.

## Dashboard Deployment

Build and deploy the Angular production bundle:

```bash
./scripts/deploy-dashboard.sh
```

The script:

1. Loads the latest successful Bicep outputs.
2. Runs `npm ci` and the Angular production build.
3. Retrieves the Static Web App deployment token without printing it.
4. Deploys `dist/tfl-analytics-dashboard/browser`.
5. Verifies the deployed root page.

The production dashboard is:

```text
https://blue-bush-0491f9503.7.azurestaticapps.net
```

The token is used only as a process environment variable and must never be
written to `.env`, documentation, logs, or source control.

## Data-Service Deployment

The main Bicep deployment creates:

- Cosmos DB database `tfl-analytics`.
- `live-events` container partitioned by `/stationId`.
- `line-status` container partitioned by `/lineId`.
- `raw-events` change-feed transport container (partitioned by `/partitionKey`, short TTL) plus its `leases` container.
- Seven-day default TTL on the `live-events` and `line-status` containers.
- Storage Table `alerts` for active alert history.
- Azure SignalR Service Free F1 with managed-identity access.

Run the focused management-plane smoke tests after deployment:

```bash
./scripts/smoke-azure-data-services.sh
```

The script verifies free-tier controls, Cosmos throughput and TTL, partition
keys, disabled local authentication, and the expected managed-identity role
assignments. It does not retrieve account keys or connection strings.

## Workload RBAC

The `workload-rbac` module grants only the data-plane roles needed by each
workload:

| Workload identity | Scope | Role |
|---|---|---|
| API | Key Vault | Key Vault Secrets User |
| Ingestion Function | Key Vault | Key Vault Secrets User |
| Processing Function | Key Vault | Key Vault Secrets User |

The Event Hubs sender/receiver assignments were removed with the Cosmos
change-feed migration (2026-06-27). The Cosmos DB data-plane roles (raw-events
writes and change-feed reads) are granted in the `cosmos` module, and the API's
`Storage Table Data Reader` role on `alerts` is granted in the `api-hosting`
module. Key Vault roles permit reading secret values but not creating, updating,
or deleting secrets.

Each host sets `AZURE_CLIENT_ID` to the matching user-assigned identity. This is
required for the Function Apps because they also have a system-assigned
identity.

Verify the assignments and identity selection:

```bash
./scripts/smoke-azure-workload-rbac.sh
```

The script checks role, principal, and scope tuples without reading any Key
Vault secret values.

## Diagnostic Settings

The `diagnostics` module sends selected operational records to the existing Log
Analytics workspace:

| Resource | Categories |
|---|---|
| Key Vault | `AuditEvent` |
| Cosmos DB | `ControlPlaneRequests` |
| SignalR | `AllLogs` |

Verbose Cosmos data-plane requests, Function application logs, and broad storage
transaction logs are intentionally excluded to control cost and avoid duplicate
telemetry.

Verify the settings:

```bash
./scripts/smoke-azure-diagnostics.sh
```

## Deployment Outputs

Display resource names without duplicating generated suffixes:

```bash
az deployment group show \
  --name tfl-foundation-dev \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --query "properties.outputs" \
  --output json
```

Load individual outputs into the current shell using the repository script:

```bash
source scripts/load-azure-outputs.sh
```

The script selects the newest successful deployment containing the expected
foundation outputs. It exports only resource and deployment names and does not
retrieve Key Vault secret values.

Override the defaults when loading outputs from another environment:

```bash
RESOURCE_GROUP=another-resource-group \
DEPLOYMENT=another-deployment \
source scripts/load-azure-outputs.sh
```

## Management-Plane Smoke Tests

These commands verify that Azure Resource Manager can find each resource and
that provisioning succeeded. They do not prove data-plane permissions.

List all resources:

```bash
az resource list \
  --resource-group "$RESOURCE_GROUP" \
  --query "[].{name:name, type:type, location:location}" \
  --output table
```

Check the storage account:

```bash
az storage account show \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{name:name, state:provisioningState, hns:isHnsEnabled, tls:minimumTlsVersion}" \
  --output table
```

Check Key Vault without reading secret values:

```bash
az keyvault show \
  --name "$KEY_VAULT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{name:name, state:properties.provisioningState, rbac:properties.enableRbacAuthorization}" \
  --output table
```

Check the Cosmos DB transport containers:

```bash
az cosmosdb sql container show \
  --account-name "$COSMOS_ACCOUNT" \
  --database-name "$COSMOS_DATABASE" \
  --resource-group "$RESOURCE_GROUP" \
  --name raw-events \
  --query "{name:name, partitionKey:resource.partitionKey.paths, ttl:resource.defaultTtl}" \
  --output table
```

Check Log Analytics:

```bash
az monitor log-analytics workspace show \
  --workspace-name "$LOG_ANALYTICS" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{name:name, state:provisioningState, retention:retentionInDays, sku:sku.name}" \
  --output table
```

Check Application Insights:

```bash
az monitor app-insights component show \
  --app "$APPLICATION_INSIGHTS" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{name:name, state:provisioningState, kind:kind, retention:retentionInDays}" \
  --output table
```

Check the deployed compute resources:

```bash
az resource show \
  --resource-group "$RESOURCE_GROUP" \
  --resource-type Microsoft.Web/sites \
  --name "$INGESTION_FUNCTION_APP" \
  --api-version 2024-04-01 \
  --query "{name:name, state:properties.state, host:properties.defaultHostName}" \
  --output table

az resource show \
  --resource-group "$RESOURCE_GROUP" \
  --resource-type Microsoft.Web/sites \
  --name "$PROCESSING_FUNCTION_APP" \
  --api-version 2024-04-01 \
  --query "{name:name, state:properties.state, host:properties.defaultHostName}" \
  --output table

az containerapp revision list \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[].{name:name, active:properties.active, health:properties.healthState, replicas:properties.replicas}" \
  --output table

curl --fail --silent --show-error "https://$API_HOSTNAME/health/live"
curl --fail --silent --show-error \
  "https://$API_HOSTNAME/api/tfl/line-status/victoria"

curl --fail --silent --show-error \
  "https://$STATIC_WEB_APP_HOSTNAME/" \
  --output /dev/null

curl --fail --silent --show-error --include --request OPTIONS \
  "https://$API_HOSTNAME/api/tfl/line-status/victoria" \
  --header "Origin: https://$STATIC_WEB_APP_HOSTNAME" \
  --header "Access-Control-Request-Method: GET"
```

The preflight response should be `204` and include
`Access-Control-Allow-Origin` for the exact Static Web App origin.

## Data-Plane Smoke Tests

Data-plane commands require suitable RBAC assignments. A resource can be
healthy even when the signed-in user is correctly denied data access.

Confirm the storage container, queues, and table using Microsoft Entra
authentication:

```bash
az storage container exists \
  --account-name "$STORAGE_ACCOUNT" \
  --name raw \
  --auth-mode login \
  --query exists

az storage queue exists \
  --account-name "$STORAGE_ACCOUNT" \
  --name processing \
  --auth-mode login \
  --query exists

az storage queue exists \
  --account-name "$STORAGE_ACCOUNT" \
  --name processing-poison \
  --auth-mode login \
  --query exists

az storage table exists \
  --account-name "$STORAGE_ACCOUNT" \
  --name audit \
  --auth-mode login \
  --query exists
```

Each command should return `true`. If it returns an authorization error, assign
the narrowest suitable Storage Blob, Queue, or Table Data role rather than
using an account key.

List Key Vault secret names without retrieving values:

```bash
az keyvault secret list \
  --vault-name "$KEY_VAULT" \
  --query "[].name" \
  --output table
```

Expected secret names:

```text
TflApi--AppKey
Datadog--ApiKey
```

Do not use `az keyvault secret show` in shared terminals, logs, screenshots, or
CI output because it can expose secret values.

## Tags

Verify the common tags:

```bash
az resource list \
  --resource-group "$RESOURCE_GROUP" \
  --query "[].{name:name, environment:tags.environment, project:tags.project, managedBy:tags.managedBy, observability:tags.observability}" \
  --output table
```

Expected values:

```text
environment=dev
project=tfl-analytics
managedBy=bicep
observability=datadog
```

## Troubleshooting

Show failed deployment operations:

```bash
az deployment operation group list \
  --name "$DEPLOYMENT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?properties.provisioningState=='Failed'].{resource:properties.targetResource.resourceName, message:properties.statusMessage.error.message}" \
  --output table
```

Verify required resource providers:

```bash
az provider show --namespace Microsoft.Storage --query registrationState
az provider show --namespace Microsoft.KeyVault --query registrationState
az provider show --namespace Microsoft.DocumentDB --query registrationState
az provider show --namespace Microsoft.OperationalInsights --query registrationState
az provider show --namespace Microsoft.Insights --query registrationState
```

All should report `Registered`.

## Current Limitations

The current Bicep foundation does not yet deploy:

- Datadog Agent hosting or the Datadog Azure Native resource.

Smoke testing those services belongs to a future observability slice after
their Bicep modules are implemented.
