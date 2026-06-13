#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$REPOSITORY_ROOT"
source "$SCRIPT_DIR/load-azure-outputs.sh"

cosmos_free_tier="$(az cosmosdb show \
  --name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query enableFreeTier \
  --output tsv)"

cosmos_database="$(az cosmosdb sql database show \
  --account-name "$COSMOS_ACCOUNT" \
  --name "$COSMOS_DATABASE" \
  --resource-group "$RESOURCE_GROUP" \
  --query name \
  --output tsv)"

cosmos_throughput="$(az cosmosdb sql database throughput show \
  --account-name "$COSMOS_ACCOUNT" \
  --name "$COSMOS_DATABASE" \
  --resource-group "$RESOURCE_GROUP" \
  --query resource.throughput \
  --output tsv)"

live_events_partition_key="$(az cosmosdb sql container show \
  --account-name "$COSMOS_ACCOUNT" \
  --database-name "$COSMOS_DATABASE" \
  --name "$COSMOS_LIVE_EVENTS_CONTAINER" \
  --resource-group "$RESOURCE_GROUP" \
  --query "resource.partitionKey.paths[0]" \
  --output tsv)"

live_events_ttl="$(az cosmosdb sql container show \
  --account-name "$COSMOS_ACCOUNT" \
  --database-name "$COSMOS_DATABASE" \
  --name "$COSMOS_LIVE_EVENTS_CONTAINER" \
  --resource-group "$RESOURCE_GROUP" \
  --query resource.defaultTtl \
  --output tsv)"

line_status_partition_key="$(az cosmosdb sql container show \
  --account-name "$COSMOS_ACCOUNT" \
  --database-name "$COSMOS_DATABASE" \
  --name "$COSMOS_LINE_STATUS_CONTAINER" \
  --resource-group "$RESOURCE_GROUP" \
  --query "resource.partitionKey.paths[0]" \
  --output tsv)"

line_status_ttl="$(az cosmosdb sql container show \
  --account-name "$COSMOS_ACCOUNT" \
  --database-name "$COSMOS_DATABASE" \
  --name "$COSMOS_LINE_STATUS_CONTAINER" \
  --resource-group "$RESOURCE_GROUP" \
  --query resource.defaultTtl \
  --output tsv)"

cosmos_role_count="$(az cosmosdb sql role assignment list \
  --account-name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --query "length(@)" \
  --output tsv)"

sql_state="$(az sql db show \
  --name "$SQL_DATABASE" \
  --server "$SQL_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query status \
  --output tsv)"

sql_free_limit="$(az sql db show \
  --name "$SQL_DATABASE" \
  --server "$SQL_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query useFreeLimit \
  --output tsv)"

sql_free_limit_behavior="$(az sql db show \
  --name "$SQL_DATABASE" \
  --server "$SQL_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query freeLimitExhaustionBehavior \
  --output tsv)"

sql_entra_only="$(az sql server ad-admin list \
  --server "$SQL_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query "[0].azureAdOnlyAuthentication" \
  --output tsv)"

signalr_sku="$(az signalr show \
  --name "$SIGNALR" \
  --resource-group "$RESOURCE_GROUP" \
  --query sku.name \
  --output tsv)"

signalr_local_auth_disabled="$(az signalr show \
  --name "$SIGNALR" \
  --resource-group "$RESOURCE_GROUP" \
  --query disableLocalAuth \
  --output tsv)"

signalr_id="$(az signalr show \
  --name "$SIGNALR" \
  --resource-group "$RESOURCE_GROUP" \
  --query id \
  --output tsv)"

signalr_role_count="$(az role assignment list \
  --scope "$signalr_id" \
  --query "length([?roleDefinitionName=='SignalR App Server'])" \
  --output tsv)"

[[ "$cosmos_free_tier" == "true" ]]
[[ "$cosmos_database" == "$COSMOS_DATABASE" ]]
[[ "$cosmos_throughput" == "1000" ]]
[[ "$live_events_partition_key" == "/stationId" ]]
[[ "$live_events_ttl" == "604800" ]]
[[ "$line_status_partition_key" == "/lineId" ]]
[[ "$line_status_ttl" == "604800" ]]
[[ "$cosmos_role_count" == "2" ]]
[[ "$sql_state" == "Online" || "$sql_state" == "Paused" ]]
[[ "$sql_free_limit" == "true" ]]
[[ "$sql_free_limit_behavior" == "AutoPause" ]]
[[ "$sql_entra_only" == "true" ]]
[[ "$signalr_sku" == "Free_F1" ]]
[[ "$signalr_local_auth_disabled" == "true" ]]
[[ "$signalr_role_count" == "2" ]]

printf '%s\n' \
  "Azure data-service smoke tests passed:" \
  "  Cosmos DB: free tier, 1000 RU/s, seven-day TTL, two data-role assignments" \
  "  Azure SQL: $SQL_DATABASE is $sql_state, Entra-only, free-limit auto-pause" \
  "  SignalR: Free_F1, local authentication disabled, two app-server roles"
