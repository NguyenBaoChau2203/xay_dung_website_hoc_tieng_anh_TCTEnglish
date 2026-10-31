# Ledger — local bootstrap LB01 + acceptance — 31/10/2026

> Subsequent owner-authorized review/fix/commit integration:
> [session closeout](2026-10-31-session-closeout-review.md). The records below
> describe the Cursor pass before that review; in particular BackupClientPath is
> now intentionally validation-only and live ASR uses an explicit opt-in/synthetic
> in-memory WAV. Preserve these historical actual counts rather than overwrite them.

Baseline SHA: `df251e642e43a30a8f784bc2cefaa2d7214a9449`
Branch: `feature/bao-chau-ui-audit`
Working tree: **uncommitted** (no commit/push/CI/PR/merge/deploy this turn)
Scope: **local only** — plan `docs/superpowers/plans/2026-10-31-local-bootstrap-and-acceptance-plan.md`
Review: `docs/feature-audits/2026-10-31-cursor-sa-rereview-and-release-status.md`

Preserve owner uncommitted AGENTS.md / rules / docs. Preserve `:5110` PID 1456,
`tctenglish-mariadb-local:3307`, acceptance `sa1031:3310`, disposable `lb01_1031:3321`,
ASR whisper `:8091`.

## Case log

| Case | Status | Provider | Command / artifact | Pass/fail/skip | Notes |
| --- | --- | --- | --- | --- | --- |
| L0 LB01 red | **pass** | PS fake docker + script unittest | `uv run … test_bootstrap_mariadb_acceptance` (8 OK); red first: `'Aa\\nBb' != 'Aa\\\\nBb'` then green after Escape-SqlLiteral | pass | Expected IDENTIFIED BY literal doubles backslash; HEX contract `41615C6E4262` vs buggy `41610A4262` |
| L1 encode + login | **pass** | MariaDB 10.5.23 `tctenglish-mariadb-acceptance-lb01_1031` `:3321` | bootstrap → login password gốc `Aa\nBb` → rerun same identity → reject foreign | pass | Escape backslash then quotes; no global sql_mode change; container kept |
| L2 G03 notify 201/450/lease | **pass (slice)** | MariaDB factory + Collection | `dotnet test -c Release --filter MariaDbNotificationWorkerAcceptanceTests` → 3 pass; dual-credit still on MariaDbCompletionConcurrency | pass | Import/outbox/Premium full matrix **not** ported (still SQLite suites) |
| L2 G01 ASR silence | **pass (slice)** | whisper.cpp `:8091` | `WhisperLiveSilenceAcceptanceTests` live silence → adapter `NoSpeech` / no `BLANK_AUDIO` | pass | Full WAV→assessment→receipt→progress→credit app E2E **blocker** |
| L2 G04 UI | **partial** | app `:5110` + bcrypt synthetic | `.tmp/ui-acceptance-5110/auth-http-matrix-lb01.json` authenticated HTTP 200 + Admin deny | partial | Not 25-view desk/mobile/keyboard/error/empty/loading/race matrix |
| L3 G06 staging seam | **partial** | workspace mariadb-dump under `.tmp` | `deploy-plesk.ps1 -ValidateOnly -BackupClientPath …` → `BackupClientExists=true`; dump 153926 B / 55 CREATE; restore to `tct_acceptance_sa1031_restore` tables/users/mig **55/208/8 = 55/208/8** | partial | Enabled backup→migrate→copy→health still **blocked** (no Program Files Plesk, no fake release zip); dump needs sandbox-line strip for 10.5 client |
| L4 verify | **pass (local)** | Release bin + scripts | `dotnet test -c Release` **733/0 fail/0 skip**; Python scripts **65 OK**; `scripts/agent_tooling.py` verified | pass | No remote CI invoked |

## Schema / deploy

- Schema impact: **none** (LB01 is bootstrap SQL literal only; no EF entity/migration).
- Hosting / Plesk / gate / production: **untouched**.

## Diff inventory (agent code vs baseline; owner docs kept)

Modified (code/tests/scripts):
- `scripts/bootstrap-mariadb-acceptance.ps1` — Escape-SqlLiteral double-backslash
- `scripts/tests/test_bootstrap_mariadb_acceptance.py` — LB01 regressions
- `scripts/deploy-plesk.ps1` — optional `BackupClientPath` (.tmp SiteRoot only)
- `scripts/tests/test_deploy_tools.py` — override reject/allow
- `TCTEnglish.Tests/Infrastructure/MariaDbAcceptanceFactory.cs` — Notifications/JobRuns wipe + goals seed
- `TCTEnglish.Tests/MariaDbCompletionConcurrencyTests.cs` — Collection serialize

Untracked (agent):
- `TCTEnglish.Tests/Infrastructure/AsrLiveAcceptanceFactAttribute.cs`
- `TCTEnglish.Tests/Infrastructure/MariaDbAcceptanceCollection.cs`
- `TCTEnglish.Tests/Infrastructure/MariaDbAcceptanceTheoryAttribute.cs`
- `TCTEnglish.Tests/MariaDbNotificationWorkerAcceptanceTests.cs`
- `TCTEnglish.Tests/WhisperLiveSilenceAcceptanceTests.cs`
- `docs/feature-audits/2026-10-31-local-bootstrap-and-acceptance-ledger.md`

Owner-preserved uncommitted (do not treat as agent deliverable alone):
- `AGENTS.md`, `.cursor/rules/database-pr-release.mdc`
- `.ai/prompts/cursor-auto-local-bootstrap-and-acceptance.md`
- `.ai/prompts/cursor-auto-transaction-bootstrap-asr-remediation.md` (edited)
- `docs/feature-audits/2026-10-31-cursor-sa-rereview-and-release-status.md`
- `docs/superpowers/plans/2026-10-31-local-bootstrap-and-acceptance-plan.md`

## Blockers (honest)

1. **G03 remainder:** SpeakingImport fingerprint/retry/Premium/atomic publish + outbox race still SQLite / not on MariaDbAcceptanceFactory (`SpeakingImportJobs` not in wipe list).
2. **G01 remainder:** no timed app path WAV→receipt→credit/reload with wrong-phrase/unavailable/cancel no-reward matrix.
3. **G04 remainder:** no full 25-view AU/CO/SP/WR/GO/AI/AD desk+mobile+keyboard matrix (HTTP 200/302 alone insufficient per plan).
4. **G06 remainder:** full enabled deploy drill needs owned staging release artifact + migration/copy/health failure inject; cannot write `C:\Program Files (x86)\Plesk\…`; default production client path unchanged unless `-BackupClientPath` + `.tmp` SiteRoot.
5. **Remote:** intentionally not run; prior CI SHA is historical only.

## Stop

Local L0–L4 executed within authority. Ready for owner review of evidence/diff/blockers. No commit/push.
