# Azure Bicep Deployment Guide

This guide covers validation, preview, deployment, output discovery, and smoke
testing for the current Phase 1 Azure foundation.

Run commands from the repository root.

## Current Foundation

The Bicep deployment currently creates:

| Resource | Development configuration |
|---|---|
| ADLS Gen2 storage | StorageV2, Standard LRS, hierarchical namespace |
| Blob container | `raw` |
| Storage queues | `processing`, `processing-poison` |
| Storage table | `audit` |
| Key Vault | Standard, RBAC authorization, seven-day soft delete |
| Event Hubs namespace | Basic, one throughput unit |
| Event hub | `tfl-events`, two partitions, one-day retention |
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
| Container registry | Private Basic ACR with managed-identity image pull |

The Function hosts and their application packages are deployed. The Angular
line-status dashboard is deployed to the Static Web App and calls the Container
App API through an origin-restricted CORS policy.

Linux App Service `P0v4` was evaluated at `$0.0913` per hour in UK South on
June 12, 2026, or approximately `$15.34` for seven continuous days. Although
within budget, Azure rejects the deployment because this subscription offer has
zero Microsoft.Web total regional VM quota. Container Apps Consumption avoids
that dedicated App Service quota and can scale the API to zero. Basic ACR was
`$0.1666` per day when checked on June 12, 2026.

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

- Event Hubs SKU or capacity.
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

ACR Tasks are disabled by this subscription offer, so build and push the Linux
image locally:

```bash
az acr login --name "$CONTAINER_REGISTRY"

docker buildx build \
  --platform linux/amd64 \
  --file src/TflAnalytics.Api/Dockerfile \
  --tag "$CONTAINER_REGISTRY_LOGIN_SERVER/tfl-analytics-api:dev" \
  --push .
```

Deploy Bicep after the image exists so the first Container App revision can pull
it using its managed identity.

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

The ingestion package currently contains its timer heartbeat and health
function. The processing package currently contains its health function; event
processing triggers are implemented in a later delivery phase.

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

Check Event Hubs:

```bash
az eventhubs namespace show \
  --name "$EVENT_HUBS_NAMESPACE" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{name:name, state:provisioningState, status:status, sku:sku.name}" \
  --output table

az eventhubs eventhub show \
  --name "$EVENT_HUB" \
  --namespace-name "$EVENT_HUBS_NAMESPACE" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{name:name, status:status, partitions:partitionCount, retention:messageRetentionInDays}" \
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
az provider show --namespace Microsoft.EventHub --query registrationState
az provider show --namespace Microsoft.OperationalInsights --query registrationState
az provider show --namespace Microsoft.Insights --query registrationState
```

All should report `Registered`.

## Current Limitations

The current Bicep foundation does not yet deploy:

- Workload RBAC beyond Function host/deployment storage access.
- Cosmos DB, Azure SQL, and SignalR.
- Datadog Agent hosting or the Datadog Azure Native resource.
- Resource diagnostic settings.

Smoke testing those services belongs to a later Phase 1 slice after their Bicep
modules are implemented.
