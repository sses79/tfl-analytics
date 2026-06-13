#!/usr/bin/env bash

# Source this file so the exported variables remain in the current shell:
# source scripts/load-azure-outputs.sh

if [[ -n "${ZSH_VERSION:-}" ]]; then
  case "${ZSH_EVAL_CONTEXT:-}" in
    *:file) ;;
    *)
      echo "Source this script instead: source scripts/load-azure-outputs.sh" >&2
      exit 1
      ;;
  esac
elif [[ -n "${BASH_VERSION:-}" && "${BASH_SOURCE[0]}" == "$0" ]]; then
  echo "Source this script instead: source scripts/load-azure-outputs.sh" >&2
  exit 1
fi

if ! command -v az >/dev/null 2>&1; then
  echo "Azure CLI is required but was not found." >&2
  return 1
fi

if ! az account show >/dev/null 2>&1; then
  echo "Azure CLI is not authenticated. Run: az login" >&2
  return 1
fi

export RESOURCE_GROUP="${RESOURCE_GROUP:-rg-tfl-analytics-dev-uk-south}"

if [[ -z "${DEPLOYMENT:-}" ]]; then
  DEPLOYMENT="$(az deployment group list \
    --resource-group "$RESOURCE_GROUP" \
    --query "sort_by([?properties.provisioningState=='Succeeded' && properties.outputs.storageAccountName.value != null], &properties.timestamp)[-1].name" \
    --output tsv)"
fi

if [[ -z "$DEPLOYMENT" ]]; then
  echo "No successful foundation deployment was found in '$RESOURCE_GROUP'." >&2
  return 1
fi

export DEPLOYMENT

get_output() {
  az deployment group show \
    --name "$DEPLOYMENT" \
    --resource-group "$RESOURCE_GROUP" \
    --query "properties.outputs.$1.value" \
    --output tsv
}

if ! az deployment group show \
  --name "$DEPLOYMENT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.provisioningState" \
  --output tsv >/dev/null; then
  echo "Deployment '$DEPLOYMENT' was not found in '$RESOURCE_GROUP'." >&2
  unset -f get_output
  return 1
fi

export STORAGE_ACCOUNT="$(get_output storageAccountName)"
export KEY_VAULT="$(get_output keyVaultName)"
export EVENT_HUBS_NAMESPACE="$(get_output eventHubsNamespaceName)"
export EVENT_HUB="$(get_output eventHubName)"
export LOG_ANALYTICS="$(get_output logAnalyticsWorkspaceName)"
export APPLICATION_INSIGHTS="$(get_output applicationInsightsName)"
export CONTAINER_REGISTRY="$(get_output containerRegistryName)"
export CONTAINER_REGISTRY_LOGIN_SERVER="$(get_output containerRegistryLoginServer)"
export CONTAINER_APPS_ENVIRONMENT="$(get_output containerAppsEnvironmentName)"
export API_APP="$(get_output apiAppName)"
export API_HOSTNAME="$(get_output apiAppHostname)"
export API_IDENTITY="$(get_output apiIdentityName)"
export INGESTION_FUNCTION_APP="$(get_output ingestionFunctionAppName)"
export INGESTION_IDENTITY="$(get_output ingestionIdentityName)"
export PROCESSING_FUNCTION_APP="$(get_output processingFunctionAppName)"
export PROCESSING_IDENTITY="$(get_output processingIdentityName)"
export STATIC_WEB_APP="$(get_output staticWebAppName)"
export STATIC_WEB_APP_HOSTNAME="$(get_output staticWebAppHostname)"
export COSMOS_ACCOUNT="$(get_output cosmosAccountName)"
export COSMOS_ENDPOINT="$(get_output cosmosEndpoint)"
export COSMOS_DATABASE="$(get_output cosmosDatabaseName)"
export COSMOS_LIVE_EVENTS_CONTAINER="$(get_output cosmosLiveEventsContainerName)"
export COSMOS_LINE_STATUS_CONTAINER="$(get_output cosmosLineStatusContainerName)"
export SQL_SERVER="$(get_output sqlServerName)"
export SQL_SERVER_FQDN="$(get_output sqlServerFqdn)"
export SQL_DATABASE="$(get_output sqlDatabaseName)"
export SIGNALR="$(get_output signalRName)"
export SIGNALR_HOSTNAME="$(get_output signalRHostname)"

unset -f get_output

printf '%s\n' \
  "Loaded Azure deployment outputs:" \
  "  RESOURCE_GROUP=$RESOURCE_GROUP" \
  "  DEPLOYMENT=$DEPLOYMENT" \
  "  STORAGE_ACCOUNT=$STORAGE_ACCOUNT" \
  "  KEY_VAULT=$KEY_VAULT" \
  "  EVENT_HUBS_NAMESPACE=$EVENT_HUBS_NAMESPACE" \
  "  EVENT_HUB=$EVENT_HUB" \
  "  LOG_ANALYTICS=$LOG_ANALYTICS" \
  "  APPLICATION_INSIGHTS=$APPLICATION_INSIGHTS" \
  "  CONTAINER_REGISTRY=$CONTAINER_REGISTRY" \
  "  CONTAINER_REGISTRY_LOGIN_SERVER=$CONTAINER_REGISTRY_LOGIN_SERVER" \
  "  CONTAINER_APPS_ENVIRONMENT=$CONTAINER_APPS_ENVIRONMENT" \
  "  API_APP=$API_APP" \
  "  API_HOSTNAME=$API_HOSTNAME" \
  "  API_IDENTITY=$API_IDENTITY" \
  "  INGESTION_FUNCTION_APP=$INGESTION_FUNCTION_APP" \
  "  INGESTION_IDENTITY=$INGESTION_IDENTITY" \
  "  PROCESSING_FUNCTION_APP=$PROCESSING_FUNCTION_APP" \
  "  PROCESSING_IDENTITY=$PROCESSING_IDENTITY" \
  "  STATIC_WEB_APP=$STATIC_WEB_APP" \
  "  STATIC_WEB_APP_HOSTNAME=$STATIC_WEB_APP_HOSTNAME" \
  "  COSMOS_ACCOUNT=$COSMOS_ACCOUNT" \
  "  COSMOS_ENDPOINT=$COSMOS_ENDPOINT" \
  "  COSMOS_DATABASE=$COSMOS_DATABASE" \
  "  COSMOS_LIVE_EVENTS_CONTAINER=$COSMOS_LIVE_EVENTS_CONTAINER" \
  "  COSMOS_LINE_STATUS_CONTAINER=$COSMOS_LINE_STATUS_CONTAINER" \
  "  SQL_SERVER=$SQL_SERVER" \
  "  SQL_SERVER_FQDN=$SQL_SERVER_FQDN" \
  "  SQL_DATABASE=$SQL_DATABASE" \
  "  SIGNALR=$SIGNALR" \
  "  SIGNALR_HOSTNAME=$SIGNALR_HOSTNAME"
