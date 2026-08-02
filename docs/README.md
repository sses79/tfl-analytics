# TfL Analytics Documentation

Use this index to distinguish current operational guidance from historical
implementation records.

## Architecture

- [Azure event and data flow](./architecture/architecture-diagram.md)
- [API and dashboard architecture](./architecture/api-dashboard-architecture.md)
- [Durable Functions alert workflow](./architecture/durable-functions-alert-workflow.md)

## Deployment

- [Azure Bicep guide](./deployment/azure-bicep.md)
- [Manual Azure deployment runbook](./deployment/manual-deployment.md)
- [Post-deployment verification](./deployment/post-deployment-verification.md)
- [Local smoke tests](./deployment/local-smoke-tests.md)
- [Continuous integration](./deployment/continuous-integration.md)

## Operations

- [Current Azure resources and running cost](./operations/azure-resource-status.md)
- [Workload RBAC](./operations/workload-rbac.md)
- [Datadog Agent](./operations/datadog-agent.md)
- [Troubleshooting](./operations/troubleshooting.md)

## Development

- [Future work plan](./development/future-plan.md)
- [Arrival batching learning guide](./development/arrival-batching-learning-guide.md)
- [API CORS configuration](./development/pi-cors.md)
- [Custom-domain CORS incident record](./development/cors.md)
- [Security and PII scan record](./development/security-pii.md)
- [Angular interview notes](./development/angular-interview.md)
- [GitHub issue automation](./development/claude-github-issue-automation.md)

## History

These files explain previous designs and cost-reduction decisions. They are not
the current deployment runbooks.

- [Cosmos DB change-feed migration](./history/cosmos-change-feed-migration.md)
- [Retired Event Hubs design](./history/event-hubs-usage.md)
- [GHCR image migration](./history/ghcr-image-migration.md)
- [Azure SQL cost investigation](./history/sql-cost-investigation.md)
- [Retired local SQL Server troubleshooting](./history/sql-server-system-permission.md)
- [Legacy Event Hubs architecture image](./images/architecture-diagram.png)

Images used by the root README and architecture documents remain in
[`images/`](./images/).
