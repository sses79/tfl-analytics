#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DASHBOARD_ROOT="$REPOSITORY_ROOT/web/tfl-analytics-dashboard"
DASHBOARD_OUTPUT="$DASHBOARD_ROOT/dist/tfl-analytics-dashboard/browser"
DASHBOARD_IMAGE="tfl-analytics-dashboard-deploy:latest"

cd "$REPOSITORY_ROOT"
source "$SCRIPT_DIR/load-azure-outputs.sh"

docker build \
  --build-arg BUILD_CONFIGURATION=production \
  --file web/tfl-analytics-dashboard/Dockerfile \
  --tag "$DASHBOARD_IMAGE" \
  .

container_id="$(docker create "$DASHBOARD_IMAGE")"
cleanup() {
  docker rm "$container_id" >/dev/null 2>&1 || true
}
trap cleanup EXIT

rm -rf "$DASHBOARD_OUTPUT"
mkdir -p "$(dirname "$DASHBOARD_OUTPUT")"
docker cp "$container_id:/usr/share/nginx/html" "$DASHBOARD_OUTPUT"

deployment_token="$(az staticwebapp secrets list \
  --name "$STATIC_WEB_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query properties.apiKey \
  --output tsv)"

if [[ -z "$deployment_token" ]]; then
  echo "Static Web App deployment token was not returned." >&2
  exit 1
fi

SWA_CLI_DEPLOYMENT_TOKEN="$deployment_token" \
  npx --yes @azure/static-web-apps-cli@2.0.9 deploy "$DASHBOARD_OUTPUT" \
    --env production \
    --no-use-keychain

unset deployment_token

curl \
  --fail \
  --retry 12 \
  --retry-all-errors \
  --retry-delay 10 \
  --silent \
  --show-error \
  --output /dev/null \
  "https://$STATIC_WEB_APP_HOSTNAME/"

echo "Dashboard deployed: https://$STATIC_WEB_APP_HOSTNAME/"
