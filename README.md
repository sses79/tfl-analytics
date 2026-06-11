# TfL Live Analytics

A .NET 10 and Angular platform for ingesting Transport for London live data,
processing it through Azure services, and presenting real-time operational
analytics.

The detailed architecture is in [Plan.md](./Plan.md). Azure account, resource,
and Datadog guidance is in [plan-resources.md](./plan-resources.md).

## Phase 1 Status

Completed:

- Multi-project .NET solution with API, Functions, contracts, application,
  infrastructure, and test boundaries.
- Angular 21 application shell.
- Dockerfiles for the API, Function apps, and Angular application.
- Docker Compose configuration for Azure emulators, SQL Server, WireMock, and
  optional Datadog Agent.
- Deterministic TfL line-status fixture and API container smoke test.
- Modular Bicep foundation deployed to UK South.
- TfL and Datadog API keys stored in Azure Key Vault.

Next Phase 1 slice:

- Add Azure compute hosting and managed identities.
- Add Cosmos DB, Azure SQL, SignalR, and Static Web Apps modules.
- Add workload RBAC assignments and diagnostic settings.
- Add CI deployment workflows.

## Repository

```text
src/
  TflAnalytics.Api/
  TflAnalytics.Application/
  TflAnalytics.Contracts/
  TflAnalytics.Infrastructure/
  TflAnalytics.Ingestion.Functions/
  TflAnalytics.Processing.Functions/
tests/
  TflAnalytics.UnitTests/
  TflAnalytics.IntegrationTests/
  TflAnalytics.AzureSmokeTests/
web/
  tfl-analytics-dashboard/
infra/
  bicep/
  local/
```

## Requirements

- .NET SDK 10
- Node.js 24
- Docker Desktop
- Azure CLI with Bicep
- Azure subscription for cloud deployment

Azure Functions and SQL Server containers run as `linux/amd64` under emulation
on Apple Silicon.

## Build And Test

```bash
dotnet build TflAnalytics.sln
dotnet test TflAnalytics.sln --no-build -m:1 --disable-build-servers
```

Build Angular:

```bash
cd web/tfl-analytics-dashboard
npm install
npm run build
```

## Local Containers

Validate Compose:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  config --quiet
```

Run the API against deterministic WireMock TfL data:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  up --build wiremock api
```

Test it:

```bash
curl http://localhost:8080/health/live
curl http://localhost:8080/api/tfl/line-status/victoria,circle
```

Start all local dependencies and application services:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  up --build
```

Add Angular:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  --profile ui \
  up --build
```

Add Datadog:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  --profile observability \
  up --build
```

Local ports:

| Service | Port |
|---|---:|
| API | `8080` |
| Angular | `4200` |
| WireMock | `8089` |
| Azurite Blob | `10000` |
| Azurite Queue | `10001` |
| Azurite Table | `10002` |
| Event Hubs AMQP | `5672` |
| Event Hubs Kafka | `9092` |
| Cosmos DB gateway | `8081` |
| SQL Server | `1433` |
| Datadog APM | `8126` |
| DogStatsD | `8125/udp` |

Stop containers while preserving volumes:

```bash
docker compose --env-file .env -f infra/local/compose.yaml down
```

## Azure Foundation

Resource group:

```text
rg-tfl-analytics-dev-uk-south
```

Deployed resources:

| Resource | Name |
|---|---|
| ADLS Gen2 storage | `sttflnhkpyupi` |
| Key Vault | `kv-tfl-nhkpyupi` |
| Event Hubs namespace | `evhns-tfl-analytics-dev-nhkpyupi` |
| Event hub | `tfl-events` |
| Log Analytics | `log-tfl-analytics-dev-nhkpyupi` |
| Application Insights | `appi-tfl-analytics-dev-nhkpyupi` |

Validate Bicep:

```bash
az bicep build --file infra/bicep/main.bicep
```

Preview changes:

```bash
az deployment group what-if \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev.bicepparam
```

Deploy:

```bash
az deployment group create \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev.bicepparam
```

## Secrets

`.env` is ignored. Start from `.env.example` and never commit real values.

Azure Key Vault currently contains:

```text
TflApi--AppKey
Datadog--ApiKey
```

The double hyphen follows the convention used to map hierarchical .NET
configuration keys into Key Vault secret names.
