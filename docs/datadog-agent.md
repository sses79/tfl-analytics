# Datadog Agent

The local Datadog Agent runs as an optional Docker Compose service and acts as
the shared telemetry collector for the API, Functions, and supporting
containers.

## Data Flow

```text
API and Functions
  |-- traces ---------> Datadog APM receiver :8126/TCP
  |-- custom metrics -> DogStatsD :8125/UDP
  `-- stdout/stderr --> Docker socket
                              |
                              v
                       Datadog Agent
                              |
                              v
                         Datadog SaaS
```

The Angular application runs in the browser and does not send telemetry to this
Agent unless browser-side Datadog RUM is configured separately.

## Compose Configuration

The `datadog-agent` service belongs to the `observability` profile:

```yaml
datadog-agent:
  image: gcr.io/datadoghq/agent:7
  profiles: ["observability"]
  environment:
    DD_API_KEY: ${DD_API_KEY:-}
    DD_SITE: ${DD_SITE:-datadoghq.eu}
    DD_ENV: local
    DD_APM_ENABLED: "true"
    DD_APM_NON_LOCAL_TRAFFIC: "true"
    DD_DOGSTATSD_NON_LOCAL_TRAFFIC: "true"
    DD_LOGS_ENABLED: "true"
    DD_LOGS_CONFIG_CONTAINER_COLLECT_ALL: "true"
  ports:
    - "8125:8125/udp"
    - "8126:8126"
```

The Docker socket is mounted read-only so the Agent can discover containers and
collect their standard output logs.

## Start The Agent

Start the default services and Datadog:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  --profile observability \
  up --build
```

Enable the UI at the same time:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  --profile ui \
  --profile observability \
  up --build
```

Compose profiles are additive. Enabling `observability` includes the Datadog
Agent in addition to services that do not declare a profile.

## Secrets

Set these values in the ignored root `.env` file:

```dotenv
DD_API_KEY=your-datadog-api-key
DD_SITE=datadoghq.eu
```

Never commit the real API key. The current default site is the Datadog EU site.

## Container Logs

These settings enable collection of container `stdout` and `stderr`:

```text
DD_LOGS_ENABLED=true
DD_LOGS_CONFIG_CONTAINER_COLLECT_ALL=true
```

The Agent can therefore collect logs from the API, Functions, WireMock, and
local emulator containers. Applications should write structured logs to
standard output and avoid logging secrets or TfL URLs containing `app_key`.

## Datadog APM

The APM receiver listens on:

```text
datadog-agent:8126
```

Application services already have common identity settings such as:

```text
DD_AGENT_HOST=datadog-agent
DD_ENV=local
DD_SERVICE=tfl-analytics-api
DD_VERSION=dev
```

These variables identify telemetry but do not create traces by themselves.
The .NET services still require the Datadog .NET Tracer to be installed and
enabled in their Docker images. Until that work is implemented, the Agent's APM
receiver can be healthy while no application traces appear in Datadog.

Do not enable Datadog and Application Insights CLR profilers in the same
process. Use one CLR profiler and send any additional manual telemetry through
OpenTelemetry or supported SDK APIs.

## DogStatsD

DogStatsD receives custom metrics at:

```text
datadog-agent:8125/UDP
```

Potential project metrics include:

```text
tfl.poll.success
tfl.poll.duration
tfl.events.published
tfl.events.processing_failures
```

The applications still need a StatsD client or compatible metrics exporter to
emit these values. Opening port `8125` does not automatically generate custom
metrics.

Keep metric tags low-cardinality. Suitable tags include:

```text
env
service
event_type
line_id
station_id
result
alert_rule
```

Do not use event IDs or vehicle IDs as metric tags.

## Smoke Tests

Check Agent health:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  exec datadog-agent agent health
```

Check Agent status, integrations, log collection, APM, and DogStatsD:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  exec datadog-agent agent status
```

Check the APM receiver:

```bash
curl --fail http://localhost:8126/info
```

DogStatsD uses UDP and does not have an HTTP request-response health endpoint.
Use `agent status` to confirm that the DogStatsD server is running.

Inspect Agent logs:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  logs --tail=200 datadog-agent
```

## Current Implementation Status

| Capability | Status |
|---|---|
| Datadog Agent Compose service | Configured |
| Datadog SaaS API key injection | Configured through `.env` |
| Container log collection | Configured |
| APM receiver | Enabled |
| DogStatsD receiver | Enabled |
| .NET automatic tracing | Not implemented yet |
| Custom application metrics | Not implemented yet |
| Browser RUM | Not implemented |

Local deterministic tests must not depend on Datadog SaaS availability. They
should validate instrumentation through local exporters or captured telemetry.
