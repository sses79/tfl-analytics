# SQL Server `/.system` Permission Error

## Symptom

SQL Server fails during startup:

```text
/opt/mssql/bin/sqlservr: Error: The system directory [/.system] could not be created.
Permission denied
```

## Cause

SQL Server 2022 runs as the non-root `mssql` user by default. Without an
explicit writable home directory, the process can attempt to create
`/.system`, which the `mssql` user cannot write.

## Fix

Configure the SQL service in `infra/local/compose.yaml` with a writable home
and working directory:

```yaml
sql:
  environment:
    HOME: /var/opt/mssql
  working_dir: /var/opt/mssql
```

If the initial startup left an incomplete local database, recreate only the
SQL container and volume:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  rm --stop --force sql

docker volume rm tfl-analytics_sql-data

docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  up -d sql
```

> Removing `tfl-analytics_sql-data` permanently deletes its local SQL data.
> Only remove it when the data can be rebuilt.

## Verification

Check the SQL startup logs:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  logs --tail=100 sql
```

SQL Server should report:

```text
SQL Server is now ready for client connections.
```

Run a query inside the container:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  exec sql sh -lc \
  '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
  -P "$MSSQL_SA_PASSWORD" -C \
  -Q "SET NOCOUNT ON; SELECT 1 AS Ready"'
```
