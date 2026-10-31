# Sentinel + acceptance ledger — 30/10/2026

Baseline: `12ccec5904b4b0aec099fadcffafd11a2f8562da` on `feature/bao-chau-ui-audit`.
Review: S01. Agent: Cursor Auto (Grok/Composer 2.5 allowlist).

| ID | Area | Status | Command / evidence | Expected / Actual | SHA | Limit |
| --- | --- | --- | --- | --- | --- | --- |
| S0 | Baseline + red orchestration | Done | `MariaDbAcceptanceOrchestrationTests` | Missing/wrong sentinel/run reject with 0 writes | `b4dd98a` | No real DELETE probe |
| S1/S01 | Verify-before-write + bootstrap | Done | Factory `PrepareFixtureAsync` verify-first; `scripts/bootstrap-mariadb-acceptance.ps1` INSERT-only recreate on labeled container | No Ensure/UPSERT before verify; foreign purpose not overwritten | `b4dd98a` | Acceptance DB only |
| S2 | Provider matrix | Done | Dual-completion ×5 on `tct_acceptance_2930` + daily XP/outbox; `NotificationWorkerBatchTests` 201/450; import lifecycle; recovery | Local provider executed; deadlock retry in `de8c714` | `de8c714` | Isolated MariaDB 3308 |
| S3/G01 | ASR loopback | Done | whisper-server b5454 + tiny.en HASH_OK; silence WAV cold/warm 200 | Engine E2E probe pass; WS≈164 MiB | `7575362` | Hosting ASR still owner |
| S3/G04 | UI `:5110` | Partial pass | App + `tct_acceptance_ui5110`; browser login desk/mobile; HTTP matrix | Public/auth shell + unauth 302 matrix; no bcrypt login deep flows | `7575362` | Owner `:5100` untouched |
| S3/G06 | Staging enabled | Blocked (exact) | `deploy-plesk.ps1` enabled against `.tmp/staging-acceptance-s4` | Throws `A database backup client is required` (no Plesk `mariadb-dump`); disabled preflight exit 0 | `7575362` | No Plesk/gate/prod |
| S4 | Full/CI | Done | Local Debug **718/718** (provider executed); CI run [41099068127](https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish/actions/runs/41099068127) on `dc92bcc` → **717 pass / 1 skip** (`MariaDbCompletionConcurrency` skipped without `TCT_ACCEPTANCE_MARIADB`); release/Plesk job **skipped** (gate false) | `dc92bcc` | No merge/gate/prod |

## Product fix from S2 red

Concurrent dual speaking completion on MariaDB hit deadlock `1213` then exposed
daily XP accounting with streak. Mitigations: `CompletionCreditService` retries
deadlock/lock-wait and returns Replay if credit already applied; `GoalsService`
recovers insert races via increment after deadlock.

## Real remaining inputs

- Plesk `mariadb-dump` / staging Windows tree with backup client for full enabled
  failure/restore drill.
- bcrypt acceptance user for authenticated browser deep flows.
- Hosting ASR endpoint / production gate remain owner decisions (gate stays false).
