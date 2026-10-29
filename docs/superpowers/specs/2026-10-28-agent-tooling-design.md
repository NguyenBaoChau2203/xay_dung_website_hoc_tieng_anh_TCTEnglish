# Agent tooling modernization

Date: 2026-10-28. Scope authorized by the owner's request to inspect, filter,
modernize and install project skills, roles, hooks and repository tools.

## Outcome and constraints

Make the project's agent guidance discoverable, traceable and appropriate for
ASP.NET Core MVC, Razor, Bootstrap 5.3, SQL Server and PowerShell on Windows.
The actual application and test targets are net9.0 / EF Core 9.0.4; the installed
SDK is 10.0.300. Correct guidance, without upgrading application dependencies.
Preserve feature boundaries, security rules and the owner's single-agent policy.
Role documents are checklists used by the same agent, never spawned workers.
Do not modify Program.cs, appsettings files, project files or application UI.

## Approaches considered

1. Install every upstream plugin: broad coverage but duplicate triggers,
   incompatible framework defaults, automatic downloads and multi-agent hooks.
2. Curate pinned source snapshots and project-specific entrypoints: selected
   because the owner explicitly delegated filtering and adaptation to this task.
3. Only link repositories: little maintenance, but does not actually install
   capabilities or fix discovery. Insufficient for the request.

## Design

- Canonical project skills: `.agents/skills/tct-*`; `.agent/skills` contains
  Antigravity compatibility entrypoints referring to those same documents.
- Retain and correct ten domain skills. Add six focused entrypoints for design
  lookup, UI review, CSS motion, Superpowers, engineering context and tool review.
- Install UI/UX Pro Max's local Python search/data; retain attributed, bounded
  reference snapshots from Superpowers, Addy Osmani, Impeccable and Emil, plus ECC.
- Produce `DESIGN.md` using Google's documented format and existing project
  colors, typography and layout; it documents the baseline, not a redesign.
- Record all eleven unique requested repositories, full commit IDs, selection
  rationale, licenses and file hashes in `.ai/tooling/sources.lock.json`.
- Add inline implementer, UI reviewer and security reviewer role checklists.
- Install the official Git MCP reference server in a locked local Python tool
  environment; restrict exposed tools to repository reads in Codex configuration.
  Do not install archived MCP servers or duplicate native filesystem access.
- Add a portable Git pre-commit hook for staged tooling validation and a separate
  tooling CI check. Hooks do not format, stage, commit, push or alter source.
- Back up replaced guidance locally and preserve rollback instructions.

## Verification and practical limits

Validate skill metadata, links, source hashes, compatibility entrypoints,
MCP configuration, staged-file behavior, hook failures and paths with spaces.
Exercise UI search and MCP initialize/list-tools/read calls. Run dotnet build and
the existing tests; report pre-existing failures separately. A new chat or a
client reload may be needed to pick up project MCP configuration. Do not claim
separate-agent skill evaluations: the owner requires one agent for all work.
