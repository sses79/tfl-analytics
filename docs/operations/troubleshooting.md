# Troubleshooting

> [Documentation index](../README.md)

Local development troubleshooting guides:

- [Cosmos DB emulator runs out of Docker disk space](./cosmos-emulator-disk-space.md)

After changing `infra/local/compose.yaml`, validate it from the repository root:

```bash
docker compose \
  --env-file .env \
  -f infra/local/compose.yaml \
  config --quiet
```
