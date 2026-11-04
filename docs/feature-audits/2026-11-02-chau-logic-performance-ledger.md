# Execution Ledger — Plan2 Logic & Performance (Cursor Auto)

**Trạng thái hiện hành03/11: ĐÓNG PHẠM VI LOCAL BE0–BE8.** Owner đã giao agent
review và sửa hết các phần local còn thiếu. Các trạng thái PARTIAL/PENDING và
Docker unavailable dưới đây là checkpoint02/11 đã được thay thế bằng
[báo cáo đóng Plan](2026-11-03-plan1-plan2-local-closeout.md).
Writing SQL/EXPLAIN/benchmark, worker/SMTP, launcher/profile, assets/traces,
MariaDB upgrade/concurrency, browser roles/mobile và ASR synthetic đã có evidence.
Mic/accent người thật, clean-PC khác, live OpenAI trả phí và hosting/staging
chưa nghiệm thu; không gọi chúng là pass. Không remote stage hoặc deploy.

- Baseline commit: `210bf2d` (master) + dirty Plan1 UI on `feature/chau-ui-performance-audit`
- Model IDE: Cursor Auto | Website AI: OpenAI `gpt-6-luna` / reasoning `low` | Whisper local
- Scope: local only — no commit/push/PR/CI remote/merge/deploy; deploy gate stays off
- Updated: 2026-11-02 (Plan2 **third-review repair** D1–D3 + BE4 Speaking paging + BE5 SMTP seam + BE6 badge probe)

---

## Verdict for owner re-review

D1–D3 closed with official `ThirdReviewProbeTests` + multipage Node D3. Fresh full suite
after repair: **851 pass / 0 fail / 11 skipped / 862** (`-c Plan2Be1`, Optimize=true).
Node submit+order **6/6**. Plan2 **still not** fully complete: BE0 scale lab, BE4 Writing/
EXPLAIN, BE5 unlock-worker suite, BE6 launcher reuse, BE7 assets, BE8 browser/ASR,
MariaDB C1 rerun (Docker engine pipe unavailable this session).

---

## Third-review D1–D3

| ID | Red | Fix | Green |
| --- | --- | --- | --- |
| D1 | Cancel after provider reply before Save → orphan assistant + Released | Isolated quota UoW (scope + SaveQuota-only); `providerReplyKnown` persist/settle/mark-pending; never Release known usage; `RecoverKnownPending` | `ThirdReviewProbeTests.CancelAfter…` draft+existing; `KnownProviderReplyPersistFailure_LeavesRecoverablePendingUsage` |
| D2 | Speaking 24/25, tail topic listed with 0 cards | SQL filter before Skip/Take; Index query params; `/Speaking/Videos` + Load more; topics remain full facet | `Speaking_CatalogFilters…` pages through public/private + topic filter |
| D3 | Page2 inserted between page1 and initial | Insert chronological fragment **after** load-older control (`nextSibling`) | Node D3 two-page + C4 single-page |

**Official probes:** `TCTEnglish.Tests/ThirdReviewProbeTests.cs` (from `.tmp/cursor-third-review/ThirdReviewTests.cs`, semantics adapted for real paging).

---

## Preflight / prior gates (still green)

| Item | Status | Evidence |
| --- | --- | --- |
| F1 Admin 991.98 | DONE | `_AdminLayout.cshtml` |
| F2 submit guard | DONE | Node **4/4** (within 6/6) |
| CursorReviewProbes (R1–R8) | DONE | Prior + retained in full suite |
| C1–C6 re-review | DONE (local SQLite/TestServer) | RepairReview 4/4; MariaDB simultaneous **unverified this session** |
| Load-older C4+D3 | DONE | Node **2/2** order tests |

---

## Batch progress

| Batch | Status | Notes |
| --- | --- | --- |
| BE0 | PARTIAL | Interceptor present; 30-sample before/after scale lab still open |
| BE1 | DONE (local) | UpdateEmail validation + concurrent reset consume |
| BE2 | DONE (local) | Quota ledger + isolated UoW + known-reply cancel lifecycle + recovery API |
| BE3 | DONE (local) | Paging + Load older multipage order fixed |
| BE4 | PARTIAL | Speaking SQL filters/paging/HasMore + Videos JSON + UI Load more. Writing SQL aggregates/EXPLAIN **not** done |
| BE5 | PARTIAL | `ISmtpMailTransport` + fake cancel/fail/success tests. AutoUnlockWorker suite still open; worker default off |
| BE6 | PARTIAL | Awarded-only profile badges + isolation probe. Launcher daily-usage reuse open |
| BE7 | PENDING | CDN pin/SRI / @import / diagnostics |
| BE8 | PARTIAL | Full suite green locally; browser roles/mobile/ASR/clean-PC **unverified**; MariaDB blocked |

---

## Commands (this session)

```
dotnet test TCTEnglish.Tests -c Plan2Be1 -p:Optimize=true
→ Passed 851 / Failed 0 / Skipped 11 / Total 862

dotnet test … --filter FullyQualifiedName~ThirdReviewProbeTests|SmtpTransportFakeTests
→ Passed 8 / Failed 0

node --test scripts/tests/test_ai_chat_submit_guard.cjs scripts/tests/test_ai_chat_load_older_order.cjs
→ 6/6 pass

docker ps / MariaDB acceptance
→ Docker Linux engine pipe unavailable (same class of blocker as reviewer vòng3)
```

No new EF migration this round (D1–D3/BE4 Speaking paging/BE5 transport/BE6 probe = no schema).
Prior additive quota migrations unchanged. `dotnet ef … has-pending-model-changes` needs
local User Secrets / connection — not re-proven without secrets this handoff; no entity
mapping edits in this repair.

---

## Still open

1. BE0: warmup5 + 30-sample query/byte/latency lab on synthetic scale.
2. BE4: Writing status aggregates + SQL LIMIT per filter; EXPLAIN MariaDB.
3. BE5: AutoUnlockWorker idempotency suite (default off).
4. BE6: launcher usage reuse / request-scoped cache.
5. BE7: pin Marked/SweetAlert + cascade + traces.
6. BE8: browser Standard/Premium/Admin Plan1; ASR opt-in; clean-PC if available.
7. MariaDB C1 simultaneous reserve / upgrade / preservation when Docker engine is up.
8. Migrations apply only on authorized disposable/dev — **not** production.

---

## Explicit non-claims

- Not claimed: Plan2 complete, hosting SLA, browser/ASR acceptance, remote CI/deploy,
  MariaDB proof this session.
- Not claimed: BE2/BE4 whole-batch DONE beyond the listed local evidence.
- Plan1 UI preserved; no commit/push/PR this session.

## Owner review checkpoint — vòng 3 repair, 02/11/2026

Repair executed locally on `feature/chau-ui-performance-audit`. D1–D3 red→green with
promoted regressions. Continued BE4 Speaking paging, BE5 SMTP transport seam, BE6 badge
isolation probe. Full suite **851/0/11/862**. Docker MariaDB unverified. Ready for
owner local review — no remote stages.

- [Review vòng 3](2026-11-02-cursor-plan2-third-review.md)
- [Repair plan](../superpowers/plans/2026-11-02-cursor-plan2-third-review-repair.md)
- [Cursor execution prompt](../../.ai/prompts/cursor-plan2-third-review-repair.md)
