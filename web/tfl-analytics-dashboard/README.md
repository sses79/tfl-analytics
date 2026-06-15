# TfL Analytics Dashboard

Angular 21 dashboard for live Transport for London line status. The first
dashboard slice monitors Victoria, Circle, Central, Jubilee, and Piccadilly
lines and refreshes every 60 seconds.

## Local Development

Use the repository Docker Compose stack for local development and verification.
The dashboard image builds inside Linux and avoids the macOS/Node 24 esbuild
deadlock seen with host `npm run build`.

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  --profile ui \
  up --build
```

Run the command from the repository root, not from this directory. Open
`http://localhost:4200`. The Docker build uses the Angular development
configuration, so browser calls go to the local API and SignalR hub at
`http://localhost:8080`.

## Build And Test

Recommended local build:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  --profile ui \
  build web
```

Host npm commands are useful for package maintenance and tests only:

```bash
npm install
npm test -- --watch=false
```

Do not use host `npm run build` as the local verification path on macOS. Static
Web App routing and security headers are configured in
`public/staticwebapp.config.json`.

## Azure Deployment

From the repository root:

```bash
./scripts/deploy-dashboard.sh
```

The script performs a clean production build, reads the existing Azure resource
names from Bicep outputs, deploys to Azure Static Web Apps, and smoke-tests the
published root page.

Live dashboard:

```text
https://blue-bush-0491f9503.7.azurestaticapps.net
```
