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

Test the API health endpoint, OpenAPI document, and the API-to-WireMock TfL
request:

```bash
curl --fail http://localhost:8080/health/live
curl --fail http://localhost:8080/openapi/v1.json
curl --fail http://localhost:8080/api/tfl/line-status/victoria,circle
```

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

Check both Azure Functions hosts. These checks prove host liveness; trigger
execution needs an integration test or trigger-specific log assertion:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  logs --tail=100 ingestion-functions processing-functions
```

Each host should report `Application started` and `Now listening on`.

## Angular

When the `ui` profile is running, check Angular:

```bash
curl --fail --output /dev/null \
  --write-out 'Angular HTTP %{http_code}\n' \
  http://localhost:4200/
```

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

The local integration and Azure smoke-test projects currently contain skipped
placeholders; the unit test project runs normally.
