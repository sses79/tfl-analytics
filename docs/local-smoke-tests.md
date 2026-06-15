# Local Smoke Tests

Run these commands from the repository root after starting the required Docker
Compose services.

## Container Status

Check that all expected containers are running:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  ps -a
```

## API

Test the API health endpoint, OpenAPI document, API-to-WireMock request, and
Phase 5 query endpoints:

```bash
curl --fail http://localhost:8080/health/live
curl --fail http://localhost:8080/openapi/v1.json
curl --fail http://localhost:8080/api/tfl/line-status/victoria,circle
curl --fail http://localhost:8080/api/stations
curl --fail http://localhost:8080/api/lines/status
curl --fail http://localhost:8080/api/stations/940GZZLUVIC/arrivals
curl --fail http://localhost:8080/api/alerts
curl --fail http://localhost:8080/api/dashboard/summary
```

The query endpoints read from the local Cosmos DB and SQL Server containers,
not from Azure.

## WireMock

Test the WireMock fixture directly:

```bash
curl --fail http://localhost:8089/Line/victoria,circle/Status
```

WireMock loads mappings at startup. Restart it after editing a mapping:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  restart wiremock
```

## Azurite

Check that the Blob, Queue, and Table endpoints accept TCP connections:

```bash
nc -z localhost 10000
nc -z localhost 10001
nc -z localhost 10002
```

An unsigned HTTP request to Azurite normally returns `403`, which still proves
that the service is listening:

```bash
curl --output /dev/null \
  --write-out 'Azurite Blob HTTP %{http_code}\n' \
  'http://localhost:10000/devstoreaccount1?comp=list'
```

## Event Hubs

Check Event Hubs emulator health and its AMQP and Kafka listeners:

```bash
curl --fail http://localhost:5300/health
nc -z localhost 5672
nc -z localhost 9092
```

The ingestion Function publishes deterministic arrival observations every 30
seconds and line-status observations every two minutes to the `tfl-events`
event hub. WireMock serves the configured arrival, stop metadata, and
line-status fixtures, so this path does not require a live TfL API key.

## Cosmos DB

Check Cosmos DB emulator readiness:

```bash
curl --fail http://localhost:8082/ready
```

The response should report `"ready": true` and healthy `postgres`, `gateway`,
and `explorer` checks.

## SQL Server

Run a query inside the container without exposing the password:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  exec sql sh -lc \
  '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
  -P "$MSSQL_SA_PASSWORD" -C \
  -Q "SET NOCOUNT ON; SELECT 1 AS Ready"'
```

## Azure Functions

Check both Azure Functions hosts:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  logs --tail=100 ingestion-functions processing-functions
```

Each host should report `Application started` and `Now listening on`.

After at least two minutes, ingestion logs should show successful arrival and
line-status timer executions. Processing logs should show the Event Hubs archive
trigger and queue processor completing for both event types.

## Angular

When the `ui` profile is running, check Angular:

```bash
curl --fail --output /dev/null \
  --write-out 'Angular HTTP %{http_code}\n' \
  http://localhost:4200/
```

Open `http://localhost:4200` and confirm the header reports both the API and
live updates as online. Arrival, line-status, and alert messages are relayed
from the processing Function to development-only API endpoints and then
broadcast through the self-hosted SignalR hub.

## Datadog

When the `observability` profile is running, check the Datadog Agent and its
APM receiver:

See the [Datadog Agent guide](./datadog-agent.md) for configuration, data flow,
and current instrumentation limitations.

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  exec datadog-agent agent health

curl --fail http://localhost:8126/info
```

DogStatsD uses UDP and has no request-response health endpoint. Check that the
agent reports DogStatsD as running:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  exec datadog-agent agent status
```

## Automated Tests

Run the current automated test suite:

```bash
dotnet test TflAnalytics.sln \
  --no-restore \
  -m:1 \
  --disable-build-servers
```

The local integration test is opt-in so ordinary test runs remain independent
of Docker. The Azure smoke-test project remains a placeholder.

To verify the complete running ingestion path, provide host-reachable emulator
connection strings and enable the opt-in local test:

```bash
RUN_LOCAL_STACK_TESTS=true \
LOCAL_STORAGE_CONNECTION_STRING='<Azurite connection string using localhost>' \
LOCAL_COSMOS_CONNECTION_STRING='<Cosmos emulator connection string using localhost>' \
dotnet test \
  tests/TflAnalytics.IntegrationTests/TflAnalytics.IntegrationTests.csproj \
  --no-restore \
  --no-build \
  -m:1 \
  --disable-build-servers
```

This asserts that compressed raw events exist in the `raw` Blob container and
that arrival and line-status documents exist in their Cosmos DB containers.

To verify the Phase 4 alert path, start the processing stack and provide
host-reachable Event Hubs, Azurite, Cosmos, and SQL connection strings:

```bash
RUN_PHASE4_LOCAL_STACK_TESTS=true \
LOCAL_EVENT_HUBS_CONNECTION_STRING='<Event Hubs emulator connection string using localhost>' \
LOCAL_STORAGE_CONNECTION_STRING='<Azurite connection string using localhost>' \
LOCAL_COSMOS_CONNECTION_STRING='<Cosmos emulator connection string using localhost>' \
LOCAL_SQL_CONNECTION_STRING='<SQL Server connection string using localhost>' \
dotnet test \
  tests/TflAnalytics.IntegrationTests/TflAnalytics.IntegrationTests.csproj \
  --no-restore \
  --no-build \
  -m:1 \
  --disable-build-servers \
  --filter LineDisruptionRunsTheAlertWorkflow
```

This publishes an isolated good-service observation followed by a disruption
and verifies one SQL alert plus its Table Storage audit record. If SQL reports
an `sa` login failure after the password changed, recreate only the local
`sql-data` volume so SQL Server initializes with the current `.env` password.
