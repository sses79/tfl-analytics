# Manual Azure Deployment Runbook

TfL Analytics uses repository scripts and Azure CLI for controlled development
deployments. GitHub Actions validates pull requests but does not deploy Azure
resources.

Run all commands from the repository root.

## Prerequisites

Install and authenticate:

```bash
az login
az account set --subscription "TfL Analytics Development"
docker version
dotnet --version
node --version
npm --version
```

Confirm the active Azure context:

```bash
az account show \
  --query "{subscription:name, subscriptionId:id, tenantId:tenantId}" \
  --output table
```

The target resource group is:

```text
rg-tfl-analytics-dev-uk-south
```

## 1. Verify The Repository

Run the local checks before publishing:

```bash
dotnet build TflAnalytics.sln --no-restore -m:1 --disable-build-servers
dotnet test TflAnalytics.sln \
  --no-restore \
  --no-build \
  -m:1 \
  --disable-build-servers

cd web/tfl-analytics-dashboard
npm ci
npm run build
npm test -- --watch=false
cd ../..

az bicep build --file infra/bicep/main.bicep

MSSQL_SA_PASSWORD='Compose_validation_only_123!' \
docker compose \
  --env-file .env.example \
  -f infra/local/compose.yaml \
  config --quiet

./scripts/security-scan.sh
git diff --check
git status --short
```

## 2. Choose An API Image Tag

Use an immutable tag such as the Git commit SHA:

```bash
API_IMAGE_TAG="$(git rev-parse --short=12 HEAD)"
echo "$API_IMAGE_TAG"
```

Do not deploy the mutable `latest` tag.

Update `apiImageTag` in:

```text
infra/bicep/environments/dev.bicepparam
```

Example:

```bicep
param apiImageTag = 'a7e525d12345'
```

## 3. Build And Push The API

Load the current Azure resource names:

```bash
source scripts/load-azure-outputs.sh
```

Sign in to the registry and push an AMD64 image:

```bash
az acr login --name "$CONTAINER_REGISTRY"

docker buildx build \
  --platform linux/amd64 \
  --file src/TflAnalytics.Api/Dockerfile \
  --tag "$CONTAINER_REGISTRY_LOGIN_SERVER/tfl-analytics-api:$API_IMAGE_TAG" \
  --push .
```

The image must exist before Bicep updates the Container App revision.

## 4. Preview And Validate Bicep

Always compile and run `what-if`:

```bash
az bicep build --file infra/bicep/main.bicep

az deployment group what-if \
  --name "manual-preview-$(date +%Y%m%d-%H%M%S)" \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev.bicepparam
```

Review creates, modifications, and deletes. Stop if the preview contains:

- An unexpected resource deletion.
- A paid SKU or capacity increase.
- Reduced retention or security controls.
- A replacement of a stateful resource.

Azure may mark nested modules as ignored when parameters contain runtime
resource references. Run ARM validation as the additional gate:

```bash
az deployment group validate \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev.bicepparam \
  --output none
```

## 5. Deploy Azure Resources

Use a unique deployment name:

```bash
DEPLOYMENT_NAME="manual-$(date +%Y%m%d-%H%M%S)"

az deployment group create \
  --name "$DEPLOYMENT_NAME" \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev.bicepparam \
  --output table
```

Confirm success:

```bash
az deployment group show \
  --name "$DEPLOYMENT_NAME" \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --query "{name:name,state:properties.provisioningState,timestamp:properties.timestamp}" \
  --output table
```

## 6. Deploy Function Packages

Publish, zip, deploy, and health-check both Function Apps:

```bash
./scripts/deploy-functions.sh
```

This updates:

- `func-tfl-analytics-ingestion-dev-nhkpyupi`
- `func-tfl-analytics-processing-dev-nhkpyupi`

The development namespace uses Event Hubs Basic, so the Azure processing
trigger must use the built-in `$Default` consumer group. Creating additional
consumer groups requires Standard tier and must not be introduced without a
separate cost review.

## 7. Deploy The Dashboard

Build and deploy the Angular production bundle:

```bash
./scripts/deploy-dashboard.sh
```

The script retrieves the Static Web App deployment token without printing or
persisting it.

Dashboard:

```text
https://blue-bush-0491f9503.7.azurestaticapps.net
```

## 8. Run Azure Smoke Tests

Load the newest successful deployment outputs:

```bash
source scripts/load-azure-outputs.sh
```

Verify the deployed infrastructure:

```bash
./scripts/smoke-azure-data-services.sh
./scripts/smoke-azure-workload-rbac.sh
./scripts/smoke-azure-diagnostics.sh
```

Verify the application endpoints:

```bash
curl --fail --silent --show-error \
  "https://$API_HOSTNAME/health/live"

curl --fail --silent --show-error \
  "https://$API_HOSTNAME/api/tfl/line-status/victoria,circle"

curl --fail --silent --show-error \
  "https://$INGESTION_FUNCTION_APP.azurewebsites.net/api/health"

curl --fail --silent --show-error \
  "https://$PROCESSING_FUNCTION_APP.azurewebsites.net/api/health"

curl --fail --silent --show-error \
  "https://$STATIC_WEB_APP_HOSTNAME/" \
  --output /dev/null
```

## Rollback

For an API rollback, set `apiImageTag` to the last known healthy immutable tag,
then repeat Bicep compilation, `what-if`, validation, and deployment.

For Function or dashboard rollback, check out the last known healthy commit and
rerun the corresponding deployment script.

Do not use `git reset --hard`, delete the resource group, or manually delete
stateful resources as a routine rollback.

## Cost Review

Before each deployment, verify that the preview preserves:

- Cosmos DB lifetime free tier.
- Azure SQL free-limit auto-pause.
- SignalR Free F1.
- Static Web Apps Free.
- Container Apps scale-to-zero and maximum two replicas.
- Basic ACR and Event Hubs capacity one.
- Narrow diagnostic categories rather than verbose request logging.

After deployment, review Azure Cost Management and keep the seven-day project
spend below the agreed GBP 100 limit.
