#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SCAN_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/tfl-security-scan.XXXXXX")"

cleanup() {
  rm -rf "$SCAN_ROOT"
}

trap cleanup EXIT

cd "$REPOSITORY_ROOT"

git clone --no-local --no-checkout . "$SCAN_ROOT/repository" >/dev/null
cp .gitleaks.toml "$SCAN_ROOT/repository/.gitleaks.toml"

docker run --rm \
  --volume "$SCAN_ROOT/repository:/repo:ro" \
  zricethezav/gitleaks@sha256:c00b6bd0aeb3071cbcb79009cb16a60dd9e0a7c60e2be9ab65d25e6bc8abbb7f \
  git /repo \
  --config /repo/.gitleaks.toml \
  --redact \
  --no-banner

dotnet list TflAnalytics.sln package --vulnerable --include-transitive

(
  cd web/tfl-analytics-dashboard
  npm audit --omit=dev
)

echo "Security scan completed successfully."
