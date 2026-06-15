# Phase 4 Status

Completed:

- Versioned arrival and line-status observation contracts with deterministic
  event IDs.
- Typed TfL arrivals, stop metadata, and line-status operations with bounded
  transient retries.
- Configurable 30-second arrival and two-minute line-status timer triggers.
- Event Hubs publication through connection strings for the local emulator and
  managed identity in Azure.
- Deterministic WireMock fixtures for arrivals, stop metadata, and line status.
- Azure ingestion settings for monitored stations, Tube lines, schedules,
  Event Hubs, and the Key Vault-backed TfL API key.
- Event Hub-triggered raw event archiving as compressed JSON with Hive-style
  event, date, station, and line partitions.
- Lightweight Storage Queue processing messages and queue-triggered validation.
- Idempotent Cosmos DB persistence for arrivals and line status with seven-day
  TTL and duplicate conflict handling.
- Queue retry and poison-message behavior through the Functions host.
- Opt-in local integration coverage that verifies raw archives and both Cosmos
  containers against the running Docker stack.
- Azure deployment verified with live timer polling, raw Blob archives, queue
  processing, and arrival and line-status documents in Cosmos DB.
- Prediction-slippage and good-to-disrupted line-status alert rules.
- Durable Functions orchestration with retried SQL persistence, Table Storage
  audit, and mock-notification activities.
- Idempotent SQL alert inserts and deterministic orchestration instance IDs.
- Opt-in local end-to-end coverage from Event Hubs through Cosmos history,
  Durable Functions, SQL Server, and Azurite Table Storage.
- Multi-project .NET solution with API, Functions, contracts, application,
  infrastructure, and test boundaries.
- Angular 21 live line-status dashboard with loading, error, refresh, and
  disruption states.
- Dockerfiles for the API, Function apps, and Angular application.
- Docker Compose configuration for Azure emulators, SQL Server, WireMock, and
  optional Datadog Agent.
- Deterministic TfL line-status fixture and API container smoke test.
- Modular Bicep foundation deployed to UK South.
- TfL and Datadog API keys stored in Azure Key Vault.
- Two .NET 10 Flex Consumption Function Apps and a Free Static Web App deployed.
- API image stored in a private Basic ACR and deployed to Azure Container Apps
  Consumption with scale-to-zero enabled.
- Azure API health and live TfL line-status smoke tests completed.
- Ingestion and processing Function packages deployed and health-checked.
- Angular dashboard deployed to Azure Static Web Apps and connected to the
  Container App API through an explicit CORS policy.
- Cosmos DB, Azure SQL, and Azure SignalR development services deployed with
  free-tier guards and managed-identity access.
- Event Hubs sender/receiver and Key Vault secret-reader roles assigned to the
  API and Function workload identities.
- Selected Azure diagnostic settings deployed to Log Analytics.
- GitHub Actions validates .NET, Angular, Bicep, scripts, Compose, secrets, and
  dependencies.
- Manual Azure release and rollback workflow documented.

Phases 1 through 4 are complete and deployed. The Azure Phase 4 workflow was
verified with a controlled line-status transition through Event Hubs, Blob and
Queue Storage, Cosmos DB, Durable Functions, Azure SQL, and Table Storage.

## Next Phase

- Implement Phase 5 API, SignalR, and dashboard features.
