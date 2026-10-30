# Execution ledger — final acceptance remediation 30/10

Branch: `feature/bao-chau-ui-audit`. Baseline HEAD: `54f0c149ed5a80975c2893d10281ea1dc0a1cc9b`.
Started: 2026-10-30 (UTC+7). Agent: Cursor Auto (Grok/Composer 2.5 allowlist).

Initial source hashes (SHA256):

| File | Hash |
| --- | --- |
| WritingAssessmentPolicy.cs | DAD1CEBB9F7044EE8E379692B641C306178B2712FFB0410300C309A3F2978930 |
| WhisperHttpTranscriber.cs | 0561F53A83EF9C350971F32DBC5968A70F82F3C7320F53E9683298E1E562EA25 |
| publish-plesk-release.py | 993E2D3124EEEF26D6B85F517C6B8B77A9D3F69D70DFA679C0BB96470D50BF5E |
| deploy-plesk.ps1 | 2DE15F7414D372E07A4CA7F9DC3787D192F6F610C0D89A3A5584D19EAAB0ABB5 |

Review/plan/prompt docs were untracked at start; preserved (no reset/stash).
CodeGraph: local CLI `scripts/codegraph.ps1` (MCP codegraph tools not exposed).
Release app `bin/Release` locked by running TCTEnglish PID; tests used Debug layout.

| ID | File / area | Status | Command | Expected / Actual | SHA | Artifact / limit |
| --- | --- | --- | --- | --- | --- | --- |
| C0 | Baseline + red regressions | Done | git status/rev-parse; CodeGraph explore | Branch/docs preserved; regressions added | 54f0c14 | Ledger created |
| C1 | REV01 Writing | Done | `dotnet test … WritingLocalAssessment|WritingNormalization|WritingQualityCorpus` Debug | Counterexamples NeedsReview; exact still Accepted; corpus ≥40 | (pending commit) | No schema migration |
| C2 | REV03 ASR schema | Done | `dotnet test … WhisperHttpTranscriberTests|SpeakingInferenceLimiterTests` | invalid_schema; NoSpeech only for valid empty text; limiter still green | (pending commit) | No network engine |
| C3 | REV02/04 marker+publisher | Done | `ReleaseMarkerHttpTests`; `unittest … test_deploy_tools` | Marker JSON + no-store; reuse/mismatch without create | (pending commit) | Program.cs + scripts only |
| C4 | G03 MariaDB | Done (partial scope) | Isolated `tctenglish-mariadb-acceptance-2930` :3308; `MariaDbCompletionConcurrencyTests` ×3 | One credit under dual completion barrier | — | Did **not** touch `tctenglish-local-mariadb-data`. Notification/import lease races: Not run this pass |
| C5 | G01/G02 engine+corpus | Partial | Writing corpus theory; ASR notes | G02 corpus automated pass; G01 engine **Not run** | — | `docs/feature-audits/2026-10-30-asr-engine-acceptance.md` |
| C6 | G04 UI 25 views | Not run | — | Matrix draft only | — | Port 5110 not started; owner 5100 untouched |
| C7 | G05 full suite + CI | In progress | Debug suite **695 passed / 0 failed**; Python 51 OK; Node behavior OK | Expect CI on feature SHA; release skipped | — | Release bin lock → Debug |
| C8 | G06 release drill + handoff | Partial | Disabled deploy preflight tests pass | Full enabled Windows staging drill **Not run**; production **Blocked** (gate false, no creds rotation) | — | No Plesk/SQL hosting writes |

## Acceptance summary

| Item | Result |
| --- | --- |
| REV01 | Closed in code/tests |
| REV02 | Closed in code/tests (wwwroot marker + HTTP) |
| REV03 | Closed in code/tests |
| REV04 | Closed in script/tests (idempotent reuse) |
| G01 | Not run (engine pin/hash/license not downloaded) |
| G02 | Closed for local corpus labeling; optional LT/Ollama Deferred default-off |
| G03 | Partial — dual completion on isolated MariaDB pass; broader worker matrix Not run |
| G04 | Not run (browser) |
| G05 | Local full suite green; CI to be dispatched on push |
| G06 | Production Blocked; local disabled-release guards green |

No package/target/provider changes. No production merge/gate/deploy.
