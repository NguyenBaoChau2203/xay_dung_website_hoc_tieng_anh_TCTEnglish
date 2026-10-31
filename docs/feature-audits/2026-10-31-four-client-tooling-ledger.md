# Four-client tooling alignment — 31/10/2026

Owner authorized inline implementation, grouped commits, PR/merge to master and
workspace synchronization in this session. Production deployment remains excluded.
Baseline df251e6; private original-diff snapshot captured before edits. A single
agent performed implementation/review; no other AI client was used as a worker.

## Implemented and measured

| Area | Before | After / evidence |
| --- | --- | --- |
| Core instructions | AGENTS.md22289 UTF8 bytes /344 lines | 10343 bytes; core owner/security/DB/release/verification invariants retained |
| Claude startup text | AGENTS+README+CLAUDE29063 bytes | AGENTS+CLAUDE11538 bytes; README routing on demand |
| Diagnostic | Separate checks required interpretation | node scripts/agent-tools.cjs doctor [--json], read-only metadata and explicit native-unverified status |
| Canonical skills | 16 | 16 retained; Claude/legacy Antigravity adapters retained for compatibility |
| Project MCP routes | Shared pinned launcher | Confirmed all4 match, project CodeGraph1.6.2; no global CLI upgrade |
| Local index policy | Checkout-approved auto-sync | Preserved; no new main-index initialization or rebuild |

Byte reductions describe text footprint, **not** billed tokens, model quality or
measured end-to-end cost savings. Architecture tables remain available in
docs/project-structure.md; operational/provenance details in existing runbooks.
Core policy now explicitly preserves granted owner stage authority instead of
requiring repeated approvals. Production still requires separate authorization.

## Client matrix

| Client | Project MCP/hooks | Live evidence / limitations |
| --- | --- | --- |
| Codex | Matched shared definitions | codegraph_explore called in this chat; project/read-only protocol checks passed |
| Cursor | Matched shared definitions | Hook command/contract passed; active catalog/native UI not observed |
| Claude Code | Matched shared definitions; single core import | Hook contract passed; personal PATH-based CodeGraph is also a candidate; actual memory/MCP/trust UI not observed |
| Antigravity | Matched .agents configs and legacy adapters | Hook contract passed; global PATH-based CodeGraph1.6.0 is a route candidate, native precedence not observed |

Personal/global configs and native trust were not rewritten. The existing
workspace route remains the reviewed way to use1.6.2. Doctor reports personal
route candidates without credentials/URLs/env values or claiming they override.
Reload/reconnect the intended workspace server if a host still retains an older
session. Config/contract checks do not prove every open IDE has reloaded.

## Validation

- New doctor tests:4/4 pass; no process execution/file writes, sanitized malformed
  personal config and correct import footprint; red before adding implementation.
- Node bootstrap+CodeGraph policy:19/19 pass.
- Four host/four event/malformed-input hook command contracts: pass.
- Git MCP initialize/list/status:7 tools; unadvertised git_add call denied.
- CodeGraph1.6.2 read-only smoke: pass; main DB/WAL fingerprint unchanged.
- Approved project-mode smoke: pass; explore-only and current source.
- Auto-sync fixture: pass; add/edit/delete, debounce, catch-up, shared daemon,
  path/tool scope and return to read-only; fixture cleanup kept app/main index.
- Final Python suite72/72 and tooling validator passed, including the reviewed
  acceptance scripts; final application suite746/746 passed separately with live local
  ASR/disposable MariaDB. Tooling checks do not certify those application results.

Artifacts are ignored under .tmp/session-closeout. Team usage and doctor limits
are documented in docs/team-agent-setup.md. Only repository tooling was changed;
no model allowlist, plugins, API keys, personal trust or remote services changed.
