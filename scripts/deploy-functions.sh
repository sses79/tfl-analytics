#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-$REPOSITORY_ROOT/artifacts/functions}"

cd "$REPOSITORY_ROOT"

source "$SCRIPT_DIR/load-azure-outputs.sh"

publish_and_package() {
  local project="$1"
  local service="$2"
  local publish_dir="$ARTIFACTS_DIR/$service/publish"
  local package="$ARTIFACTS_DIR/$service.zip"

  rm -rf "$publish_dir"
  mkdir -p "$publish_dir"

  dotnet publish "$project" \
    --configuration Release \
    --output "$publish_dir"

  if [[ ! -f "$publish_dir/host.json" ]]; then
    echo "Published package for '$service' does not contain host.json." >&2
    return 1
  fi

  rm -f "$package"
  (
    cd "$publish_dir"
    zip -q -r "$package" .
  )

  PACKAGE_RESULT="$package"
}

deploy_package() {
  local app_name="$1"
  local package="$2"

  echo "Deploying $(basename "$package") to $app_name..."
  az functionapp deployment source config-zip \
    --resource-group "$RESOURCE_GROUP" \
    --name "$app_name" \
    --src "$package" \
    --timeout 600 \
    --output none
}

smoke_test() {
  local app_name="$1"
  local expected_service="$2"
  local endpoint="https://$app_name.azurewebsites.net/api/health"
  local response

  response="$(curl \
    --fail \
    --retry 12 \
    --retry-all-errors \
    --retry-delay 10 \
    --silent \
    --show-error \
    --max-time 60 \
    "$endpoint")"

  if [[ "$response" != *"\"service\":\"$expected_service\""* ]] ||
     [[ "$response" != *"\"status\":\"healthy\""* ]]; then
    echo "Unexpected health response from '$app_name': $response" >&2
    return 1
  fi

  echo "$app_name: $response"
}

publish_and_package \
  src/TflAnalytics.Ingestion.Functions/TflAnalytics.Ingestion.Functions.csproj \
  ingestion
ingestion_package="$PACKAGE_RESULT"

publish_and_package \
  src/TflAnalytics.Processing.Functions/TflAnalytics.Processing.Functions.csproj \
  processing
processing_package="$PACKAGE_RESULT"

deploy_package "$INGESTION_FUNCTION_APP" "$ingestion_package"
deploy_package "$PROCESSING_FUNCTION_APP" "$processing_package"

smoke_test "$INGESTION_FUNCTION_APP" tfl-analytics-ingestion
smoke_test "$PROCESSING_FUNCTION_APP" tfl-analytics-processing
