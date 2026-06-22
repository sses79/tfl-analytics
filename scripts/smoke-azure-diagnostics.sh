#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$REPOSITORY_ROOT"
source "$SCRIPT_DIR/load-azure-outputs.sh"

if [[ -z "$LOG_ANALYTICS" ]]; then
  echo "Azure diagnostic-setting smoke tests are not applicable: observability is disabled."
  exit 0
fi

workspace_id="$(az monitor log-analytics workspace show \
  --workspace-name "$LOG_ANALYTICS" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

assert_diagnostics() {
  local resource_id="$1"
  shift
  local expected_categories=("$@")
  local actual_workspace_id
  local normalized_actual_workspace_id
  local normalized_workspace_id
  local category
  local enabled

  actual_workspace_id="$(az monitor diagnostic-settings show \
    --name operational \
    --resource "$resource_id" \
    --query workspaceId \
    --output tsv)"

  normalized_actual_workspace_id="$(printf '%s' "$actual_workspace_id" | tr '[:upper:]' '[:lower:]')"
  normalized_workspace_id="$(printf '%s' "$workspace_id" | tr '[:upper:]' '[:lower:]')"

  [[ "$normalized_actual_workspace_id" == "$normalized_workspace_id" ]]

  for category in "${expected_categories[@]}"; do
    enabled="$(az monitor diagnostic-settings show \
      --name operational \
      --resource "$resource_id" \
      --query "logs[?category=='$category'].enabled | [0]" \
      --output tsv)"

    if [[ "$enabled" != "true" ]]; then
      echo "Diagnostic category '$category' is not enabled for '$resource_id'." >&2
      exit 1
    fi
  done
}

key_vault_id="$(az keyvault show \
  --name "$KEY_VAULT" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

event_hubs_id="$(az eventhubs namespace show \
  --name "$EVENT_HUBS_NAMESPACE" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

cosmos_id="$(az cosmosdb show \
  --name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

signalr_id="$(az signalr show \
  --name "$SIGNALR" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

sql_database_id="$(az sql db show \
  --name "$SQL_DATABASE" \
  --server "$SQL_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

assert_diagnostics "$key_vault_id" AuditEvent
assert_diagnostics "$event_hubs_id" DiagnosticErrorLogs OperationalLogs
assert_diagnostics "$cosmos_id" ControlPlaneRequests
assert_diagnostics "$signalr_id" AllLogs
assert_diagnostics "$sql_database_id" Errors Timeouts Deadlocks DevOpsOperationsAudit

echo "Azure diagnostic-setting smoke tests passed for five resources."
