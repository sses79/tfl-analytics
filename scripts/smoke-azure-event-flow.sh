#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$REPOSITORY_ROOT"

if [[ -z "${API_BASE_URL:-}" ]]; then
  source "$SCRIPT_DIR/load-azure-outputs.sh"
  API_BASE_URL="https://$API_HOSTNAME"
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required for the Azure event-flow smoke test." >&2
  exit 1
fi

expected_line_count="${EXPECTED_LINE_COUNT:-11}"
maximum_age_seconds="${MAXIMUM_EVENT_AGE_SECONDS:-1200}"
api_base_url="${API_BASE_URL%/}"

line_status_json="$(curl --fail --silent --show-error "$api_base_url/api/lines/status")"
summary_json="$(curl --fail --silent --show-error "$api_base_url/api/dashboard/summary")"

actual_line_count="$(jq 'length' <<<"$line_status_json")"
summary_line_count="$(jq '.linesMonitored' <<<"$summary_json")"
last_event_utc="$(jq -r '.lastEventUtc // empty' <<<"$summary_json")"

if [[ "$actual_line_count" -ne "$expected_line_count" ]]; then
  echo "Expected $expected_line_count line-status records but received $actual_line_count." >&2
  exit 1
fi

if [[ "$summary_line_count" -ne "$expected_line_count" ]]; then
  echo "Dashboard summary reports $summary_line_count monitored lines; expected $expected_line_count." >&2
  exit 1
fi

if ! jq -e 'map(.lineId) | index("waterloo-city") != null' <<<"$line_status_json" >/dev/null; then
  echo "The line-status response does not include waterloo-city." >&2
  exit 1
fi

if [[ -z "$last_event_utc" ]]; then
  echo "Dashboard summary does not contain lastEventUtc." >&2
  exit 1
fi

normalized_last_event="$(sed -E 's/\.[0-9]+\+00:00$/Z/' <<<"$last_event_utc")"
last_event_epoch="$(jq -nr --arg value "$normalized_last_event" '$value | fromdateiso8601')"
current_epoch="$(date -u +%s)"
event_age_seconds="$((current_epoch - last_event_epoch))"

if [[ "$event_age_seconds" -lt 0 || "$event_age_seconds" -gt "$maximum_age_seconds" ]]; then
  echo "Latest line-status event is ${event_age_seconds}s old; maximum allowed age is ${maximum_age_seconds}s." >&2
  exit 1
fi

printf '%s\n' \
  "Azure event-flow smoke test passed:" \
  "  lineStatusCount=$actual_line_count" \
  "  waterlooCityPresent=true" \
  "  lastEventUtc=$last_event_utc" \
  "  eventAgeSeconds=$event_age_seconds" \
  "  maximumAgeSeconds=$maximum_age_seconds"
