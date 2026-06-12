# Repository Agent Guide

## Project Context

This repository implements a real-time Transport for London analytics platform
using .NET 10, Angular 21, Azure, Docker-based local emulators, Bicep, and
Datadog.

Read these files before substantial work:

- `README.md` for current commands and deployed resource names.
- `Plan.md` for architecture, delivery phases, and current implementation status.
- `plan-resources.md` for Azure, security, cost, and Datadog decisions.

The project is currently completing Phase 1. Do not implement later phases
implicitly unless the user requests them.

## Architecture Boundaries

Keep dependencies directed as follows:

```text
Contracts <- Application <- Infrastructure <- API / Functions
```

- `TflAnalytics.Contracts`: transport-neutral event contracts and shared DTOs.
- `TflAnalytics.Application`: use-case interfaces and business orchestration.
- `TflAnalytics.Infrastructure`: TfL and Azure SDK implementations.
- `TflAnalytics.Api`: HTTP endpoints and web-host concerns only.
- `TflAnalytics.Ingestion.Functions`: polling and event publication.
- `TflAnalytics.Processing.Functions`: normalization, persistence, and workflows.

Do not place Azure SDK types in Contracts or business rules in controllers.
Prefer existing interfaces and dependency-injection patterns over direct client
construction.

## Standard Verification

Run the narrowest relevant checks, then the full checks for cross-project changes:

```bash
dotnet build TflAnalytics.sln --no-restore -m:1 --disable-build-servers
dotnet test TflAnalytics.sln --no-restore --no-build -m:1 --disable-build-servers
```

For Angular changes:

```bash
cd web/tfl-analytics-dashboard
npm run build
```

For local infrastructure changes:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  config --quiet
```

For Bicep changes:

```bash
az bicep build --file infra/bicep/main.bicep
az deployment group what-if \
  --resource-group rg-tfl-analytics-dev-uk-south \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/environments/dev.bicepparam
```

Always run `what-if` before an Azure deployment. Summarize resources and likely
cost impact before creating or changing billable services.

## Local Development

- Use WireMock fixtures for deterministic tests by default.
- Do not make integration tests depend on live TfL data, Datadog SaaS, or Azure.
- Use the real TfL API only through an explicit live-development configuration.
- Preserve compatibility with Apple Silicon. Azure Functions and SQL Server
  containers currently require `linux/amd64` emulation.
- Do not remove emulator abstractions merely because an Azure SDK can connect
  directly to a cloud resource.

## Secrets And Telemetry

- Never read secret values aloud or include them in logs, diffs, commits, test
  output, or responses.
- `.env` and `local.settings.json` must remain ignored.
- Commit placeholders only through `.env.example` and
  `local.settings.example.json`.
- Azure secrets belong in `kv-tfl-nhkpyupi`.
- Current Key Vault secret names are `TflApi--AppKey` and `Datadog--ApiKey`.
- Suppress or sanitize URLs containing TfL `app_key`.
- Avoid high-cardinality Datadog tags such as event IDs and vehicle IDs.
- Do not enable overlapping Datadog and Application Insights CLR profilers.

## Azure Conventions

- Subscription environment: `TfL Analytics Development`.
- Resource group: `rg-tfl-analytics-dev-uk-south`.
- Default region: `uksouth`.
- Resource names should come from Bicep outputs rather than being duplicated in
  application code.
- Apply the common tags `environment`, `project`, `managedBy`, and
  `observability`.
- Prefer managed identity and RBAC over connection strings.
- Keep development SKUs and retention settings cost-conscious.

## Editing And Delivery

- Keep changes scoped to the requested phase and update `Plan.md` status when a
  delivery milestone changes.
- Update `README.md` when commands, ports, deployed names, or prerequisites
  change.
- Add focused tests for new contracts, mappings, alert rules, and persistence
  behavior.
- Do not replace the live Swagger-derived DTO approach with
  `Tfl.Api.Presentation.Entities.dll`; it is a legacy .NET Framework assembly.
- Before committing, run:

```bash
git diff --check
git status --short
```

- Never commit `.env`, generated build output, Azure credentials, API keys, or
  Datadog keys.
