#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$REPOSITORY_ROOT"
source "$SCRIPT_DIR/load-azure-outputs.sh"

api_identity="$(az identity show \
  --name "$API_IDENTITY" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{principalId:principalId,clientId:clientId}" \
  --output json)"

ingestion_identity="$(az identity show \
  --name "$INGESTION_IDENTITY" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{principalId:principalId,clientId:clientId}" \
  --output json)"

processing_identity="$(az identity show \
  --name "$PROCESSING_IDENTITY" \
  --resource-group "$RESOURCE_GROUP" \
  --query "{principalId:principalId,clientId:clientId}" \
  --output json)"

api_principal_id="$(jq -r .principalId <<<"$api_identity")"
api_client_id="$(jq -r .clientId <<<"$api_identity")"
ingestion_principal_id="$(jq -r .principalId <<<"$ingestion_identity")"
ingestion_client_id="$(jq -r .clientId <<<"$ingestion_identity")"
processing_principal_id="$(jq -r .principalId <<<"$processing_identity")"
processing_client_id="$(jq -r .clientId <<<"$processing_identity")"

event_hub_scope="$(az eventhubs eventhub show \
  --name "$EVENT_HUB" \
  --namespace-name "$EVENT_HUBS_NAMESPACE" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

key_vault_scope="$(az keyvault show \
  --name "$KEY_VAULT" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

storage_account_id="$(az storage account show \
  --name "$STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

alerts_table_scope="$(az resource show \
  --ids "$storage_account_id/tableServices/default/tables/alerts" \
  --api-version 2023-05-01 \
  --query id \
  --output tsv)"

assert_role() {
  local scope="$1"
  local principal_id="$2"
  local role_name="$3"
  local count

  count="$(az role assignment list \
    --scope "$scope" \
    --query "length([?principalId=='$principal_id' && roleDefinitionName=='$role_name'])" \
    --output tsv)"

  if [[ "$count" != "1" ]]; then
    echo "Expected one '$role_name' assignment for principal '$principal_id'." >&2
    exit 1
  fi
}

assert_role "$event_hub_scope" "$ingestion_principal_id" "Azure Event Hubs Data Sender"
assert_role "$event_hub_scope" "$processing_principal_id" "Azure Event Hubs Data Receiver"
assert_role "$key_vault_scope" "$api_principal_id" "Key Vault Secrets User"
assert_role "$key_vault_scope" "$ingestion_principal_id" "Key Vault Secrets User"
assert_role "$key_vault_scope" "$processing_principal_id" "Key Vault Secrets User"
assert_role "$alerts_table_scope" "$api_principal_id" "Storage Table Data Reader"

api_configured_client_id="$(az containerapp show \
  --name "$API_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.template.containers[0].env[?name=='AZURE_CLIENT_ID'].value | [0]" \
  --output tsv)"

ingestion_configured_client_id="$(az functionapp config appsettings list \
  --name "$INGESTION_FUNCTION_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?name=='AZURE_CLIENT_ID'].value | [0]" \
  --output tsv)"

processing_configured_client_id="$(az functionapp config appsettings list \
  --name "$PROCESSING_FUNCTION_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?name=='AZURE_CLIENT_ID'].value | [0]" \
  --output tsv)"

[[ "$api_configured_client_id" == "$api_client_id" ]]
[[ "$ingestion_configured_client_id" == "$ingestion_client_id" ]]
[[ "$processing_configured_client_id" == "$processing_client_id" ]]

printf '%s\n' \
  "Azure workload RBAC smoke tests passed:" \
  "  Ingestion identity: Event Hubs sender and Key Vault secret reader" \
  "  Processing identity: Event Hubs receiver and Key Vault secret reader" \
  "  API identity: Key Vault secret reader and alerts-table data reader" \
  "  All hosts select the matching user-assigned identity through AZURE_CLIENT_ID"
