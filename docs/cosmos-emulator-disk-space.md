# Cosmos DB Emulator Docker Disk Space Error

## Symptom

The Cosmos DB emulator fails while initializing PostgreSQL:

```text
mkdir: cannot create directory '/data/db': No space left on device
Postgres failed to start
```

## Cause

Docker Desktop's virtual disk has no free space. This is separate from the
host filesystem's reported free space because Docker stores images, build
cache, containers, and volumes in its own virtual disk.

Inspect Docker disk usage:

```bash
docker system df
```

## Fix

Remove unused Docker build cache:

```bash
docker builder prune --force
```

In the original incident this recovered approximately 13.15 GB. This command
does not remove application volumes.

If Cosmos failed during its first initialization, recreate only its container
and volume:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  rm --stop --force cosmos

docker volume rm tfl-analytics_cosmos-data

docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  up -d cosmos
```

> Removing `tfl-analytics_cosmos-data` permanently deletes its local Cosmos
> data. Only remove it when the data can be rebuilt.

## Verification

Check container health:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  ps -a
```

Check Cosmos readiness:

```bash
curl --fail http://localhost:8082/ready
```

The response should report `"ready": true` and healthy `postgres`, `gateway`,
and `explorer` checks.

Review startup logs if readiness fails:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  logs --tail=100 cosmos
```
