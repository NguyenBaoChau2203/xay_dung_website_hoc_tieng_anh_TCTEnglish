# Project CodeGraph installation

Authorized scope: install colbymchenry/codegraph for this project. Use one agent;
preserve the existing uncommitted toolkit work, Git MCP and lifecycle hooks.

## Design decisions

- Existing global CLI: 1.6.0. Existing index: 497 files, engine version 1.5.0.
- Reviewed release: v1.6.2, commit 6560052a6f856855d3f71eee838fd66ccfa4285d.
- Install @colbymchenry/codegraph@1.6.2 project-locally with npm lockfile and
  integrity hashes. Do not run installers that rewrite all detected IDEs or
  automatically grant tool permissions. Preserve machine-wide installation.
- Connect one CodeGraph explore MCP tool in the existing project Codex config.
- Owner rules prohibit automatic init/index/sync. The standard upstream MCP
  startup calls sync even with --no-watch; use its pinned readOnly MCPEngine
  through a small project adapter, without watcher/daemon/query-pool workers.
- Disable telemetry and opportunistic downloads for these project launchers.
- Use the existing index. Verify its main database hash before and after MCP
  smoke queries; do not change semantic index contents or start a rebuild.
- Do not modify Program.cs, appsettings, project files or application code.

## Checklist

- [x] Inspect existing CLI/index and upstream release, package and MCP source.
- [x] Install exact npm package and lockfile without lifecycle install scripts.
- [x] Add local CLI/readonly MCP launchers and project config.
- [x] Record source provenance and add installation/usage guidance.
- [x] Extend static tooling checks and meaningful tests for package/config.
- [x] Verify CLI version, MCP initialize/list/explore and index preservation.
- [x] Run tooling tests/build, review inline and report actual activation status.

## Verification ledger

- npm installed two host packages with --ignore-scripts. The lockfile also pins
  the optional bundles for other supported platforms with integrity digests.
- Local CLI reports 1.6.2. The global CLI remains 1.6.0.
- The configured MCP launcher started from TCTEnglish/, initialized as 1.6.2,
  exposed only codegraph_explore and returned real BaseController C# source.
- Database and nonempty WAL SHA-256 values stayed identical across the MCP
  lifecycle. A real read-only SQLite fixture showed that an empty WAL can appear
  without any data change; the fingerprint now normalizes absent/empty WAL.
- CodeGraph source commit/license recorded; registry now has 12 sources/84 files.
- Static tooling checks and 29/29 unit tests passed, including package pins,
  missing integrity, extra MCP tools, mutable references and network fallbacks.
  SQLite regressions preserve detection of main-file and nonempty WAL changes.
- Solution build passed with 0 warnings/0 errors; full .NET tests passed 557/557.
- codex mcp get codegraph --json reports enabled=true and the expected project
  launcher. The current chat has not yet exposed the new tool namespace; do not
  claim a native chat-tool invocation. Reload may be needed in the client.
- No app/test source changes, global upgrades, commits, pushes or index rebuilds.

## Compatibility boundary

The adapter uses internal engine/session/transport modules from a fixed release.
Future version updates require an integration check before changing the pin.
Read-only graph queries may report a stale index. That is explicit rather than
silently indexing. Existing CLI supports indexing later when the owner requests it.
