# Security and PII Scan Record

Last reviewed: June 13, 2026

## Scope

The scan covered:

- All tracked files.
- All five Git commits and their metadata.
- NuGet direct and transitive dependencies.
- Angular production and development dependencies.
- Ignored-file rules for local secrets and generated artifacts.
- GitHub repository security settings.
- Azure Bicep and public endpoint configuration.

The ignored local `.env` file was not read or mounted into a scanner container.

## Results

No live API keys, cloud credentials, connection strings, private keys, bearer
tokens, or personal data files were found in tracked content or Git history.

Gitleaks initially detected the standard Azurite `devstoreaccount1` key. This is
a fixed, publicly documented emulator value and cannot access an Azure account.
It is narrowly allowlisted in `.gitleaks.toml`.

Git commit metadata contains the author's personal email address in the first
five public commits. This disclosure is known and accepted. Git history will not
be rewritten for this item. Future commits use the GitHub noreply address:

```text
2291991+sses79@users.noreply.github.com
```

NuGet dependencies reported no known vulnerabilities.

The Angular build chain initially reported advisories through its transitive
esbuild dependency. The repository overrides esbuild to a patched release.
Production and development npm audits now report zero vulnerabilities.

## Repository Protections

The public GitHub repository has:

- Secret scanning enabled.
- Push protection enabled.
- Dependabot vulnerability alerts enabled.
- Dependabot security updates enabled.
- A scheduled and pull-request security workflow.

GitHub non-provider secret patterns and validity checks were requested but
remain unavailable or disabled for the current repository/account configuration.

## Local Protections

- `.env`, `local.settings.json`, generated Bicep JSON, build output, and
  deployment packages are ignored.
- Docker Compose requires `MSSQL_SA_PASSWORD` to be explicitly set.
- Real TfL and Datadog keys must remain in `.env` locally and Key Vault in Azure.
- Security findings are redacted by the Gitleaks scan.

## Repeat The Scan

Run the complete local scan from the repository root:

```bash
./scripts/security-scan.sh
```

The script:

1. Creates a temporary clone containing committed Git history only.
2. Runs Gitleaks without exposing ignored local files.
3. Audits all NuGet dependencies.
4. Audits production npm dependencies.

For a full npm audit including development tooling:

```bash
cd web/tfl-analytics-dashboard
npm audit
```

Before every commit, also run:

```bash
git diff --check
git status --short
```

Never commit `.env`, Azure credentials, API keys, Datadog keys, deployment
profiles, generated packages, or diagnostic output containing secret values.
