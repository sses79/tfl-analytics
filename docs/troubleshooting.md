# Troubleshooting

Local development troubleshooting guides:

- [SQL Server `/.system` permission error](./sql-server-system-permission.md)
- [Cosmos DB emulator runs out of Docker disk space](./cosmos-emulator-disk-space.md)

After changing `infra/local/compose.yaml`, validate it from the repository root:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  config --quiet
```
