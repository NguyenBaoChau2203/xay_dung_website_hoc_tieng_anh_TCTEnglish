# Agent Tooling Modernization Implementation Plan

> Follow-up correction: this first pass omitted agent lifecycle hooks while
> marking its Git-hook scope complete. The omission and remedy are recorded in
> 2026-10-28-agent-lifecycle-hooks.md. Four lifecycle definitions now exist and are
> script-tested; host trust/automatic invocation must be reported separately.

> Execute inline with one agent, following the owner's repository policy.

**Goal:** Install a curated, pinned and verifiable project agent toolkit.
**Architecture:** Native project skills, compatibility adapters, attributed source
references, a locked Git MCP environment and deterministic Git/CI checks.
**Tech stack:** Markdown, Python 3.13 standard library, uv, PowerShell, Git, .NET.
**Spec:** `docs/superpowers/specs/2026-10-28-agent-tooling-design.md`.

## Global constraints

- One agent performs investigation, implementation, review and verification.
- No edits to application source, Program.cs, appsettings or project files.
- Existing checkout reused on branch `codex/modernize-agent-tooling`.
- External sources are reviewed data; project/owner rules take precedence.
- No machine-wide plugin, MCP, skill or permission changes.

## Tasks

- [x] Inspect current guidance, real project targets, hooks, tools and CodeGraph.
- [x] Inspect all eleven unique upstream repositories and compare integration fit.
- [x] Capture baseline structural failures and add meaningful tooling tests.
- [x] Install selected source files with licenses and immutable commit provenance.
- [x] Migrate ten domain skills and add six focused native entrypoints.
- [x] Add DESIGN.md, inline roles, source lock and selection report.
- [x] Install locked Git MCP, project config and smoke check.
- [x] Add staged Git hook, installer and tooling CI validation.
- [x] Align root guidance, Antigravity workflows and Copilot discovery.
- [x] Run structural/security checks, tooling tests, build and existing tests.
- [x] Review diff inline and write Vietnamese handoff with measured limitations.

## Review focus

Missing links/resources; duplicate skill names; React/Tailwind defaults leaking
into Razor work; reference instructions overriding the single-agent policy;
mutable MCP packages; hook checking working files instead of staged content;
non-ASCII and space-containing paths; pre-existing application test failures.

## Execution ledger

- Initial working tree was clean. No active Git hooks, role files or repo MCP
  configuration were found. Ten `.agent/skills` entrypoints use display names
  incompatible with normalized skill names; Codex discovery directory was absent.
- Source inspection is performed on local shallow checkouts under ignored
  `.tmp/tooling-modernization/sources`; no upstream installer hooks are run.
- Ruling: use adapted reference installations for process/review packs. This
  preserves useful source guidance without enabling their delegation or CLI
  bootstrap behavior. The owner's request authorizes selecting and installing.
- Ruling: skill evaluation is an inline scenario review plus executable tooling
  checks, not separate-model pressure testing (prohibited by owner policy).
- Installed: sixteen canonical skills, sixteen compatibility adapters, three
  inline role lenses, five updated workflows and five corrected templates.
  Eleven repositories reviewed; 83 source files retained with hashes/licenses.
- Source review: retained UI search uses standard-library local CSV lookup;
  design-system persistence is optional and was not used in smoke queries.
  Upstream test fixtures were moved to ignored local storage. LF normalization
  and two whitespace cleanups are recorded in the source lock.
- Review: root policy overrides upstream delegation and framework defaults.
  New namespaces use TCTEnglish; verified legacy OperationResult namespaces/API
  are documented rather than blindly renamed. No application/test source changes.
- Verification: 16/16 native skills pass the skill-creator validator with UTF-8;
  10/10 tooling tests pass, including staged source corruption and unstaged fixes.
- Hook verification: a full staged snapshot passes and a corrupted staged source
  fails. A separate temporary index preserves the user's actual staging. The
  complete new-file diff also passes whitespace checks.
- MCP verification: locked sync, PowerShell launcher from an application
  subdirectory, initialize/list_tools/git_status all pass. The live host exposed
  exactly seven tct_git read tools and git_status succeeded through the chat tool.
- UI lookup: both targeted UX search and design-system generation ran successfully
  without persisting output into application assets or DESIGN.md.
- Build: final solution build passed, 0 warnings / 0 errors.
- Application baseline: 555/557 passed; two failures were ObjectDisposedException
  on SQLitePCL.sqlite3_stmt. Rerunning the two corresponding test groups passed
  5/5. Report as intermittent test failures, not verified application defects or
  a clean full suite. No runtime fixes were made under this tooling request.
- CodeGraph: installed CLI 1.6.0, existing index from 1.5.0; no automatic re-index.
- Final state: branch codex/modernize-agent-tooling; no commit/push/deployment.
  Setup, selection decisions, limits and rollback documented in docs/agent-tooling.md.
