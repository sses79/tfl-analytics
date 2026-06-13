# TfL Analytics Dashboard

Angular 21 dashboard for live Transport for London line status. The first
dashboard slice monitors Victoria, Circle, Central, Jubilee, and Piccadilly
lines and refreshes every 60 seconds.

## Local Development

Start the API on port `8080`, then run:

```bash
npm install
npm start
```

Open `http://localhost:4200`. Development builds use
`src/environments/environment.development.ts` and call
`http://localhost:8080`.

## Build And Test

```bash
npm run build
npm test -- --watch=false
```

Production builds use `src/environments/environment.ts` and call the Azure
Container App API. Static Web App routing and security headers are configured
in `public/staticwebapp.config.json`.

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
