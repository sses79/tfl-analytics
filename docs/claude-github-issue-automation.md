# Auto-implementing GitHub issues with Claude → PR into `dev`

**Status:** proposed setup (not yet implemented as of 2026-06-26).

## Goal

A hands-off path where a GitHub issue describing development work (e.g. "implement
feature X") is picked up by Claude, which writes the code and opens a PR — from a
feature branch into the `dev` integration branch — so existing CI
(`backend` / `dashboard` / `infrastructure` / `secrets` / `dependencies`)
validates it before a human reviews and merges.

## Why Claude Code GitHub Action (not Claude Desktop)

Claude Desktop is interactive and cannot be triggered by GitHub events. The
`anthropics/claude-code-action` runs on GitHub-hosted runners and reacts to issue
events, which is exactly this workflow. The repo has **no existing Claude
integration**, CI already runs on every `pull_request` (so Claude's `dev` PRs get
checked automatically), and `dev`/`main` already exist with `main` protected.

## Decisions

- **Auth:** Claude subscription via `CLAUDE_CODE_OAUTH_TOKEN` (no separate API bill).
- **Trigger:** label-gated — Claude runs only when an issue is labeled
  `claude-implement`. Keeps humans in control of which issues are "dev-ready" and
  bounds cost.
- **PR target:** feature branch `claude/issue-<n>-<slug>` → base `dev` (never `main`).

## Setup steps

### 1. Mint the subscription token and store it as a repo secret
- Locally: run `claude setup-token` (interactive OAuth) to mint a long-lived token.
- Store it: `gh secret set CLAUDE_CODE_OAUTH_TOKEN --app actions` (paste the token),
  or via GitHub → repo Settings → Secrets and variables → Actions.

### 2. Install the Anthropic GitHub App on the repo
- Easiest: run `claude` locally and use `/install-github-app`. It installs the app,
  can scaffold the workflow, and wires the secret. **Use what it scaffolds as the
  baseline**, then apply the customizations in step 3 (label trigger + `dev` base).
- Manual alternative: install "Claude" from the GitHub Marketplace/Apps onto
  `sses79/tfl-analytics` with access to issues, contents, and pull requests.

### 3. Add `.github/workflows/claude-issue.yml`
Trigger on `issues: labeled`, gate on the `claude-implement` label, and instruct
Claude to base work on `dev`. Treat the YAML below as a starting point and
reconcile the action version/input names with whatever `/install-github-app`
scaffolds (the action's inputs have changed across releases — pin the current
`anthropics/claude-code-action` major version it generates).

```yaml
name: Claude · implement labeled issue
on:
  issues:
    types: [labeled]

jobs:
  implement:
    if: github.event.label.name == 'claude-implement'
    runs-on: ubuntu-latest
    permissions:
      contents: write
      pull-requests: write
      issues: write
      id-token: write
    steps:
      - uses: actions/checkout@v5
        with:
          ref: dev              # base feature work on the integration branch
          fetch-depth: 0
      - uses: anthropics/claude-code-action@v1
        with:
          claude_code_oauth_token: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}
          prompt: |
            You are implementing GitHub issue #${{ github.event.issue.number }}:
            "${{ github.event.issue.title }}".

            Follow AGENTS.md conventions and the project's layer boundaries.
            Work on a new branch named claude/issue-${{ github.event.issue.number }}-<short-slug>
            created from dev. Implement the feature with tests.

            Before opening the PR, verify locally in the runner:
              - dotnet restore TflAnalytics.sln
              - dotnet build TflAnalytics.sln --no-restore -m:1 --disable-build-servers
              - dotnet test  TflAnalytics.sln --no-restore --no-build -m:1 --disable-build-servers
              - (if web/ changed) cd web/tfl-analytics-dashboard && npm ci && npm run build && npm test -- --watch=false
              - (if infra/ changed) az bicep build --file infra/bicep/main.bicep

            Open a pull request with BASE BRANCH = dev (gh pr create --base dev),
            titled for the feature, linking "Closes #${{ github.event.issue.number }}".
            Do NOT target main. End the commit message with the project's
            Co-Authored-By trailer.
          claude_args: "--allowedTools Bash,Edit,Write,Read"
```

Notes:
- `ref: dev` + the prompt's "create branch from dev" + `gh pr create --base dev`
  together guarantee the feature branch and PR both sit on `dev`, never `main`.
- The action's bundled `GITHUB_TOKEN` provides `gh` access for branch push + PR
  creation; the `permissions` block is the minimum needed.
- Integration tests that need the local emulators run via `infra/local/compose.yaml`
  (Azurite / Cosmos / Event Hubs emulator) and work on `ubuntu-latest`; no Azure
  secrets are required because none of the unit/integration tests call Azure.

### 4. Create the gating label
- `gh label create claude-implement --description "Hand to Claude to implement" --color 5319e7`

### 5. (Optional, recommended) Guardrails
- Add branch protection on `dev` requiring the 5 CI checks + 1 human review, so
  Claude's PRs cannot self-merge.
- Add `.github/PULL_REQUEST_TEMPLATE.md` so Claude's PRs follow a consistent shape.
- Keep `claude-implement` apply-able only by maintainers (the label is the cost gate).

## Files to add/modify
- **New:** `.github/workflows/claude-issue.yml` (the workflow above).
- **New (optional):** `.github/PULL_REQUEST_TEMPLATE.md`.
- **Repo settings (not files):** `CLAUDE_CODE_OAUTH_TOKEN` secret, Anthropic GitHub
  App install, `claude-implement` label, `dev` branch protection.
- No application code changes; this is purely repo automation/config.

## Verification (end-to-end)
1. Open a throwaway issue describing a tiny change (e.g. "add a `/version` endpoint
   returning the assembly version").
2. Apply the `claude-implement` label → confirm the **Actions** tab shows the
   `Claude · implement labeled issue` run start.
3. Confirm the run creates branch `claude/issue-<n>-…`, pushes commits, and opens a
   **PR whose base is `dev`** (not `main`), linking the issue.
4. Confirm the PR automatically triggers `ci.yml` + `security.yml` and that
   `backend` / `dashboard` / `infrastructure` / `secrets` / `dependencies` run.
5. Review the diff, merge into `dev`, and confirm the issue auto-closes.
6. Negative check: label an issue with something else (e.g. `bug`) and confirm
   **no** Claude run starts (the `if` label gate holds).

## Considerations / trade-offs
- **Cost:** every labeled issue spends subscription usage; the label gate is the
  throttle. Auto-triage of all issues was explicitly *not* chosen.
- **Security:** the workflow has `contents: write` + `pull-requests: write`. Risk is
  contained by (a) only maintainers applying the label, (b) `dev` (and `main`)
  protection requiring human review, (c) PRs never targeting `main`.
- **Quality gate:** Claude self-verifies with the repo's own build/test commands in
  the runner, and the existing CI re-runs them on the PR — two independent checks
  before a human reviews.
- **Versioning caveat:** pin the `anthropics/claude-code-action` version that
  `/install-github-app` scaffolds and re-check input names against its README, since
  they evolve between releases.
