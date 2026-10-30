# Round 2 execution ledger — 30/10/2026

Baseline HEAD: `186fc8a875b6937b3197768e8e9a753cff5f0f18` on `feature/bao-chau-ui-audit`.
Review: CR01–CR05. Agent: Cursor Auto (Grok/Composer 2.5 allowlist).

| ID | Area | Status | Command | Expected / Actual | SHA | Limit |
| --- | --- | --- | --- | --- | --- | --- |
| R0 | Baseline | Done | `git rev-parse` | 186fc8a + untracked round2 docs | 186fc8a | Preserved |
| R1/CR02 | Fixture guard | Done | `dotnet test … MariaDbAcceptanceConnectionGuardTests` | Reject remote/dev/hosting/3307 offline; ctor rejects before host | (pending push) | No unsafe DELETE probe |
| R2/CR03 | Concurrency | Done | `TCT_ACCEPTANCE_MARIADB=… MariaDbCompletionConcurrencyTests` exit 0 | Async TCS gate; overlap≥2; 1 Applied + Replay; XP/credit/outbox | — | Container `:3308` only |
| R3/CR01 | Writing | Done | WritingLocal/Corpus/Persistence focused | `<`/`>` and `1,000`≠`1.000`; old 3 cases still NeedsReview | — | Corpus label 1,5≠1.5 |
| R4/CR04 | Publisher digest | Done | `unittest scripts.tests.test_deploy_tools` | Missing digest / wrong tag SHA stop before webhook; no mock of resolve path for missing digest | — | — |
| R5/CR05 | Readiness | Done | same + marker_matches tests | run+commit+digest required; ambiguous webhook reconciles | — | Marker includes sha256 |
| R6/G01 | ASR | Partial | curl + Get-FileHash tiny.en | HASH_OK; E2E **Blocked** (no whisper-server binary/image) | — | Model ≤500MiB OK |
| R6/G03 | Matrix extra | Partial | dual-completion only | Notification/import lease matrix **Not run** | — | After CR02/03 |
| R6/G04 | UI 25 view | Not run | — | Acceptance host 5110 not started this pass | — | Owner 5100 untouched |
| R6/G06 | Staging | Not run | disabled preflight already green | Enabled Windows staging drill **Not run** | — | Gate false |
| R7 | Full/CI | In progress | Debug suite **710 pass / 1 skip** (provider skipped without env) | Local provider executed separately; CI to dispatch | — | skip ≠ pass |

## Notes

- CI historical `ff7f703`: 694 pass / 1 skip — provider did not run there.
- Round 2 does not rewrite the old ledger into “complete”; this file owns new evidence.
