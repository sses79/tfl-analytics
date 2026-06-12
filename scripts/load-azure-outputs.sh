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
  "  APPLICATION_INSIGHTS=$APPLICATION_INSIGHTS"
