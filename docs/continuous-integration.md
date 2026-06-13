# Continuous Integration

GitHub Actions validates every pull request and every push to `main`. Azure
deployment remains a manual operator action using the repository scripts and
the [manual deployment runbook](./manual-deployment.md).

## Workflows

### Security

`.github/workflows/security.yml` runs:

- Gitleaks against full Git history.
- NuGet vulnerable-package analysis.
- Production npm dependency audit.

It also runs weekly.

### CI

`.github/workflows/ci.yml` contains three independent jobs:

| Job | Checks |
|---|---|
| `backend` | .NET restore, build, and tests |
| `dashboard` | npm clean install, Angular build, and unit tests |
| `infrastructure` | Bicep compilation, shell syntax, and Docker Compose validation |

Independent jobs provide faster feedback and make failures easier to identify.

## Deployment Boundary

GitHub Actions does not receive Azure credentials and does not deploy:

- Bicep resources.
- API container images.
- Function packages.
- Static Web App content.

This keeps billable and state-changing Azure operations behind an explicit
operator review of `what-if`, cost impact, and deployment scope.

Use:

```text
docs/manual-deployment.md
```

for the complete release process.

## Pull Request Requirements

The protected `main` branch requires a pull request and successful security
checks. The CI jobs should also pass before merge:

```text
backend
dashboard
infrastructure
secrets
dependencies
```

Never bypass a failed check by pushing directly to `main`.

## Local Equivalents

Run the same checks locally:

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

bash -n \
  scripts/deploy-dashboard.sh \
  scripts/deploy-functions.sh \
  scripts/load-azure-outputs.sh \
  scripts/security-scan.sh \
  scripts/smoke-azure-data-services.sh \
  scripts/smoke-azure-diagnostics.sh \
  scripts/smoke-azure-workload-rbac.sh

MSSQL_SA_PASSWORD='Compose_validation_only_123!' \
docker compose \
  --env-file .env.example \
  -f infra/local/compose.yaml \
  config --quiet
```

## Secrets

CI uses no Azure client secret, deployment token, TfL key, or Datadog key.

The security workflow must remain read-only:

```yaml
permissions:
  contents: read
```

Do not add deployment credentials merely to make releases automatic. Revisit
GitHub OIDC only if automated Azure deployment becomes a deliberate future
requirement.
