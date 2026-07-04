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

## 2. Publish The API Image (GHCR)

The API image lives in **public GitHub Container Registry**
(`ghcr.io/sses79/tfl-analytics-api`), not Azure Container Registry. It is built
and pushed automatically by the `Publish API image` GitHub Actions workflow on
every push to `main` (and via manual `workflow_dispatch`), tagged with both
`latest` and the commit SHA. Pick the SHA of the `main` commit whose run
published the image — do not deploy the mutable `latest` tag:

```bash
API_IMAGE_TAG="d4b7caeb97de686899e0810ff7e3f5551878e649"
```

Confirm the tag exists (anonymous pull; the GHCR package must be Public):

```bash
docker manifest inspect "ghcr.io/sses79/tfl-analytics-api:$API_IMAGE_TAG" > /dev/null && echo OK
```

To build out-of-band instead of via the workflow, push to the same repo:

```bash
echo "$GITHUB_TOKEN" | docker login ghcr.io -u <github-user> --password-stdin

docker buildx build \
  --platform linux/amd64 \
  --file src/TflAnalytics.Api/Dockerfile \
  --tag "ghcr.io/sses79/tfl-analytics-api:$API_IMAGE_TAG" \
  --push .
```

## 3. Point The Deployment At The Image Tag

Set `apiImageTag` to the SHA in:

```text
infra/bicep/environments/dev.bicepparam
```

Example:

```bicep
param apiImageTag = 'd4b7caeb97de686899e0810ff7e3f5551878e649'
```

The image repository defaults to `ghcr.io/sses79/tfl-analytics-api` via the
`apiImageRepository` param in `infra/bicep/modules/api-hosting.bicep`; override it
only if the owner/repo changes. The image must exist in GHCR before Bicep updates
the Container App revision.

> For a code-free image bump (no infra change), skip Bicep and repoint the live
> app directly:
> `az containerapp update -n ca-tfl-api-dev-nhkpyupi -g rg-tfl-analytics-dev-uk-south --image ghcr.io/sses79/tfl-analytics-api:$API_IMAGE_TAG`

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

Use the complete
[Azure post-deployment verification checklist](./post-deployment-verification.md)
and update its deployment record before marking the release complete.

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
- SignalR Free F1.
- Static Web Apps Free.
- Container Apps scale-to-zero and maximum two replicas.
- The API image is on public GHCR (free); there is no Azure Container Registry.
- No Event Hubs and no Azure SQL server — Event Hubs was replaced by the Cosmos
  change feed, and the `sql` module is gated off (`enableSql=false`).
- Narrow diagnostic categories rather than verbose request logging.

After deployment, review Azure Cost Management and keep the seven-day project
spend below the agreed GBP 100 limit.
