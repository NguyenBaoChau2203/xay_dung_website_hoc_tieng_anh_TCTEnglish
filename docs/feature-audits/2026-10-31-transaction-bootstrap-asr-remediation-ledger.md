# Ledger — transaction / bootstrap / ASR remediation — 31/10/2026

Baseline SHA: `69c6815b151e3fa8c7fae0baa7086ec425d49785`
Branch: `feature/bao-chau-ui-audit`
Agent: Cursor Auto (single), allowlist Grok/Composer 2.5
Plan: `docs/superpowers/plans/2026-10-31-transaction-bootstrap-asr-remediation-plan.md`
Review: `docs/feature-audits/2026-10-31-cursor-sentinel-acceptance-review.md`

Do not rewrite historical review ledgers. Record actual provider/command/result per case.

## Environment snapshot

| Item | Value |
| --- | --- |
| HEAD at start | `69c6815` |
| Docker Desktop | `D:\DockerDesktop\Docker Desktop.exe` (started this pass) |
| Acceptance container | `tctenglish-mariadb-acceptance-sa1031` @ `127.0.0.1:3310` |
| Acceptance DBs | `tct_acceptance_sa1031`, `tct_acceptance_ui5110` (run `sa1031`) |
| Dev volume `tctenglish-mariadb-local` | untouched |
| Hosting / Plesk gate | not changed |
| Incident | First `dotnet run` without `--no-launch-profile` bound `:5100`; process stopped. Owner `:5100` may need manual restart. Acceptance restarted correctly on `:5110`. |

## Case log

| Case | Status | Provider | Command / notes | Expected | Actual | Artifact / SHA |
| --- | --- | --- | --- | --- | --- | --- |
| T0 SA01 red | Done | .NET fault injection | Ambient 1213/1205 expected goalsCalls=1 | fail before fix | Actual goalsCalls=3 | red confirmed |
| T0 SA02 red | Done | PS 5.1 + fake docker.bat | foreign run still Bootstrapped | fail | Bootstrapped foreign | red confirmed |
| T0 SA03 red | Done | WhisperHttpTranscriber | `[BLANK_AUDIO]` → NoSpeech | fail | Actual Ok | red confirmed |
| T1 SA01 fix | Done | unit + MariaDB dual-credit | no ambient retry; Goals no 1213 swallow | pass | Ambient=1 call; MariaDB DualSpeaking ×5 pass after UserGoals seed | CompletionCreditService, GoalsService |
| T2 SA02 fix | Done | bootstrap.ps1 + 6 script tests | reject foreign/port/exec; no DROP overwrite | pass | 6/6 OK | scripts/bootstrap-mariadb-acceptance.ps1 |
| T3 SA03 fix | Done | adapter unit + live engine 8091 | marker-only NoSpeech; mixed keeps Hello | pass | unit green; live text=`[BLANK_AUDIO]` | WhisperHttpTranscriber; `.tmp/asr-acceptance-2930/silence-8091-raw.json` |
| T4 G03 MariaDB | Partial | MariaDbAcceptanceFactory | dual-completion + seed goals/outbox | pass | DualSpeaking pass with TCT_ACCEPTANCE_* | NotificationWorkerBatch / Import still SQLite — not ported |
| T4 G01 ASR E2E | Partial | whisper-server 8091 + adapter | silence→NoSpeech→no commit | adapter+assessment stub NoSpeech | live silence marker + unit/assessment NoSpeech; full WAV→receipt→credit on app not fully timed | model SHA `921E4CF8…`, zip `6BA69E34…` |
| T4 G04 UI | Partial | `:5110` + bcrypt synthetic | authenticated AU/CO/SP/GO… | login + deep views | Login browser OK; auth HTTP 200 for Home/Goals/Speaking/Vocabulary/Notification/Writing/Profile; Admin 302 for student | `.tmp/ui-acceptance-5110/*` |
| T4 G06 staging | Partial/blocked | official mariadb-dump client | enabled deploy path | client provenance + dump | ZIP+dump hashed; dump OK with `--ssl=false` to acceptance DB; **cannot** install to `C:\Program Files (x86)\Plesk\MySQL\bin` (Access denied) so `deploy-plesk.ps1` enabled drill still blocked on hardcoded path | `.tmp/staging-acceptance-sa1031/` |
| T5 verify/CI | pending | build + suite + gh | | | | |

## Pins / provenance

- ASR model `ggml-tiny.en.bin` SHA256 `921E4CF8686FDD993DCD081A5DA5B6C365BFDE1162E72B08D75AC75289920B1F` (77,704,715 B), MIT
- whisper-bin-x64 b5454 ZIP SHA256 `6BA69E3482D7826214F90A6A9C84CA07782AEC1E1D0C6A7C30C994FD5D816CCB`
- MariaDB client ZIP 11.4.5 SHA256 `B7C11D38657F16B837E68199D73670510AADB78F42DFA5D5FDEA31A7AAB342E3`
- `mariadb-dump.exe` SHA256 `06FEEB5D3D25CFA4A29F1283B0DBAC8F94A789497B80DCF79D68B4E4F1FB4615`
- Synthetic users: `acc.student@tct.local` / `acc.admin@tct.local` (password only in ignored `.tmp` note)

## Schema

No EF migration required for SA01–SA03 (transaction/fixture/adapter only). Acceptance DBs migrated with repo-local `dotnet ef database update` against disposable DBs only.
