# Proposal: move the API image to GHCR, delete Azure Container Registry

**Status:** ✅ DONE (cut over 2026-07-04). Code on `dev` (commit `492ec1b`); image
published to `ghcr.io/sses79/tfl-analytics-api` (public); Container App
`ca-tfl-api-dev-nhkpyupi` revision `--0000013` runs the ghcr image; the ACR
registry entry was removed and `acrtflnhkpyupi` deleted. Verified in
`docs/post-deployment-verification.md` (July 4, 2026 record).
**Goal:** remove the last standing fixed paid resource. After the Cosmos
change-feed migration (see `docs/cosmos-change-feed-migration.md`) crushed the
variable costs, **Azure Container Registry (Basic) is now the #1 daily line at a
flat ~£0.126/day (~£3.8/mo)** — 100% "Basic Registry Unit" SKU fee, confirmed by
the cost meter (no storage overage: image is ~138 MB vs the 10 GB included; no
build minutes; no bandwidth). It's a fixed floor that can't be optimised, only
eliminated. Moving the one image it holds to **GitHub Container Registry (ghcr.io)**
takes it to **£0**.

## Security analysis (why a public image is safe here)

Two distinct questions, both green:

1. **Does the image contain secrets?** No. The Dockerfile
   (`src/TflAnalytics.Api/Dockerfile`) bakes in only compiled DLLs plus
   `appsettings.json` (TfL `AppKey` is empty) and `appsettings.Development.json`
   (localhost CORS only). All secrets/config are injected at **runtime** by the
   Container App via env vars + **Key Vault references + user-assigned managed
   identity** (`KeyVault__Name`, `AZURE_CLIENT_ID`). Nothing sensitive is in the
   image layers.
2. **Is exposure acceptable?** The repo is **already public**, so a public
   compiled image discloses strictly less than the source already on GitHub. And
   the API is **read-only** — all six endpoints are `HttpGet`
   (`api/tfl/line-status`, `api/stations`, `api/stations/{id}/arrivals`,
   `api/dashboard/summary`, `api/lines/status`, `api/alerts`). It is effectively a
   read-only feed service over public TfL data.

**Decision: publish a PUBLIC ghcr image.** The Container App then pulls with no
credentials (simplest). This move does **not** change the API's runtime exposure —
ingress is already `external: true` and unauthenticated (Entra deferred to Phase 6);
that is unchanged and out of scope here.

*(If a public image is ever undesirable, the alternative is a private ghcr image +
a GitHub PAT stored as a Container App registry secret — more to manage, and it
buys little while the repo itself is public.)*

## What changes

### 1. Build & push the image to GHCR
Add a GitHub Actions workflow `.github/workflows/publish-api-image.yml` that builds
`src/TflAnalytics.Api/Dockerfile` and pushes to
`ghcr.io/sses79/tfl-analytics-api` on push to `main` (and manual `workflow_dispatch`).
Public repo ⇒ Actions minutes and ghcr storage/pulls are free.

- Permissions: `packages: write`, `contents: read`.
- Auth: the built-in `GITHUB_TOKEN` can push to ghcr for this repo — no PAT needed.
- Tag with both `:latest` and the commit SHA (use the SHA as `apiImageTag` for
  reproducible deploys).
- After the first successful push, mark the `tfl-analytics-api` package **Public**
  in the repo's Packages settings (one-time), so the Container App can pull
  anonymously.

*(Alternative to a workflow: a local `scripts/deploy-api.sh` doing
`docker build` + `docker push ghcr.io/...`. There is currently no API deploy
script — the workflow is the cleaner home for it.)*

### 2. Point the Container App at GHCR (`infra/bicep/modules/api-hosting.bicep`)
- Change the container `image` from
  `${registry.properties.loginServer}/tfl-analytics-api:${apiImageTag}` to
  `ghcr.io/sses79/tfl-analytics-api:${apiImageTag}` (parameterise the ghcr
  repo/owner rather than hard-coding if preferred).
- **Delete** the `registries: [...]` block in the Container App `configuration`
  (public image ⇒ no registry auth).
- **Delete** the `registry` ACR resource, the `registryPullRole` role assignment,
  and the `acrPullRoleDefinitionId` var.
- **Keep** the `apiIdentity` managed identity and its **Key Vault / Table Storage /
  SignalR** access — those are still required at runtime. Only the *ACR pull* role
  is removed.
- Remove the `registryName` / `registryLoginServer` outputs (and the `registryName`
  param if it becomes unused).

### 3. Thread through `infra/bicep/main.bicep`
- Remove the `registryName: 'acrtfl${suffix}'` param wiring and any consumers of the
  removed ACR outputs. Add an `apiImageRepository` (or similar) param defaulting to
  `ghcr.io/sses79/tfl-analytics-api` if you parameterise it.

### 4. Delete the ACR resource in Azure
After the Container App is verified pulling from ghcr:
`az acr delete -n acrtflnhkpyupi -g rg-tfl-analytics-dev-uk-south`.

## Files to add/modify
- **New:** `.github/workflows/publish-api-image.yml`.
- **Modify:** `infra/bicep/modules/api-hosting.bicep` (image ref, drop ACR resource +
  pull role + registries block; keep identity + KV/Table/SignalR roles).
- **Modify:** `infra/bicep/main.bicep` (param/output wiring).
- **Repo settings (one-time):** mark the ghcr package Public.
- No application code changes.

## Cutover order (important)
1. Land + run the workflow so the image exists at `ghcr.io/sses79/tfl-analytics-api`
   and the package is Public. **Do this first** — the Container App can't pull an
   image that isn't there yet.
2. Deploy the Bicep change pointing the Container App at ghcr (image tag = the SHA
   just pushed).
3. Verify (below).
4. Only then `az acr delete` the ACR.

> **Note:** the `sql` module is already gated (`enableSql=false`, done 2026-06-27,
> verified in `docs/post-deployment-verification.md`), so a full
> `az deployment group create` is safe and will not recreate the deleted SQL
> server. For this ACR cutover, `az containerapp update` is still the lighter touch,
> but a full redeploy is no longer a landmine.

## Verification
1. Workflow run is green; `ghcr.io/sses79/tfl-analytics-api:<sha>` exists and the
   package shows **Public**.
2. After the Bicep deploy, the Container App revision goes **Ready** and its pull
   source is ghcr (`az containerapp revision list -n ca-tfl-api-dev-nhkpyupi ...`;
   check no image-pull errors in `az containerapp logs`).
3. Hit the live API: `GET /health/live` and one data endpoint (e.g.
   `GET /api/lines/status`) return 200.
4. Dashboard still loads and receives SignalR pushes.
5. Post-delete: Container Registry drops off the cost breakdown within ~2 days
   (allow for cost-data latency).

## Rollback
Revert the Bicep change (image ref back to the ACR loginServer, restore the
`registry` resource + `registryPullRole` + `registries` block) and redeploy. Keep
the change in one isolated commit so a single revert restores the ACR path.
Don't run `az acr delete` until step 3 above is green — before that, rollback is
just a Bicep revert with the ACR still present.

## Cost impact
Container Registry ~£0.126/day → **£0**. ghcr (public repo) is free for storage,
pulls, and the build workflow. Removes the last fixed paid resource; the remaining
run-rate is dominated by scale-to-zero / free-tier services.
