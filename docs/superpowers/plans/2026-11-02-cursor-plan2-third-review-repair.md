# Repair review vòng3 và hoàn tất Plan2 — Cursor Auto

02/11/2026. **PLANNED**, chỉ thực thi khi owner giao prompt.
Nhánh`feature/chau-ui-performance-audit`, HEAD210bf2d. Một agent inline, giữ
UIPlan1/BTH2/dirtywork/data và mọi workload hiện hữu.

Đọc [review3](../../feature-audits/2026-11-02-cursor-plan2-third-review.md),
ledger`docs/feature-audits/2026-11-02-chau-logic-performance-ledger.md` và active
step trong [Plan2gốc](2026-11-02-chau-logic-performance-plan.md). Các repair trước
là supporting context; không cần đọc lại toàn bộ lịch sử để làm một finding.

## Quyền và thứ tự

Giữ quyền có giới hạn Plan2: source/services/controllers/VM/tests/JS integration;
Program chỉ DI/flagskhôngsecret cần thiết. Additive schema nếu thật sự cần qua
EFskill/repo-localEF+Designer/snapshot+MariaDBupgrade/repeat/preservation+modelcheck.
Không edit appliedmigrations/history, DROP/resetdevDB, nâng framework/package tùy
tiện hoặc credentials trongGit/log/argv. WebsiteLuna/low, audioWhisper, IDEriêng.

Chỉlocal: không commit/push/PR/CIremote/merge/deploy/Plesk; gatefalse. FakeAI/SMTP,
không paid/live API. Không subagent/CLI AI/chat khác, fireforget hoặc parallelEF
samecontext. Rerunprovider chỉ disposable có đúng host/name/port/runId/sentinel.

`T0snapshot → T1(D1) → T2(D3) → T3(D2+BE4) → BE0/BE5/BE6/BE7/BE8 còn thiếu`.
BE0baseline phải có trước tối ưuquery. Nếu tạm bỏcaps đểkhôi phục correctness,
đánh dấu BE4pending rồi hoàn tấtSQLpaging theo batch tiếp, không claimperfpass.

## T0 — Snapshot và kiểm chứng không giấu gap

- [x] Status/branch/diffmanifest; giữ existingUI/docs/fixtures/workload. Không
  reset/stash/revert toàn bộ chỉ để suite dễ chạy.
- [x] Giữ old9CursorReviewProbes,4RepairReviewTests,concurrentreset,Node green.
  Fullsuite sau repair **851/0/11/862** (Plan2Be1); không dùng flags cũ thay proof.
- [x] Promote meaningfulregressions từ ignored
  `.tmp/cursor-third-review/ThirdReviewTests.cs` → `ThirdReviewProbeTests.cs`
  (paging semantics); không compile cả import tạm và test chính thức.
- [x] DockerLinuxEngine **không** kết nối được trong lượt này; MariaDB C1
  ghi unverified. Không giả MariaDB pass bằng SQLite.

## T1 — D1: quota persistence không flush caller, terminal state bao phủ knownreply

**Files:** AiChatService/quota service/interfaces/ledger; scopedDI tối thiểu;
failure/cancel/persistence tests; schema chỉ nếu design cần.

- [x] Red→green existing + draft cancel-after-provider-reply (`ThirdReviewProbeTests`).
- [x] Tách quota UoW (IServiceScopeFactory + SaveQuotaChangesAsync suspension).
- [x] Phân biệt providerReplyKnown vs replyCommitted; không Release known usage.
- [x] MarkKnownUsagePending + RecoverKnownPending recovery proof test.
- [x] Retain old Repair/Cursor/cancel regressions (focused green).

**Acceptance (local):** cancel boundary pass; no orphan; quota not Released wrongly.
MariaDB concurrency **unverified** (Docker unavailable).

## T2 — D3: history multipage và anchor ổn định

**Files:** ai-chat.js loadOlderMessages, nodeDOMtests, relevantUIintegration.
Đọc DESIGN.md +neededUIprocedure trước sửa.

- [x] Multipage Node: m4,m5 → +m2,m3 → +m0,m1 = m0..m5; control stays at top.
- [x] insertAfter control via `insertBefore(fragment, insertBefore.nextSibling)`.
- [x] C4 single-page retained; Node suite **6/6** with submit guards.
- [ ] Chrome real browser acceptance full+embed — deferred BE8.

**Acceptance (local):** Node multipage + C4 green. Browser BE8 still open.

## T3 — D2 và BE4: server pagination giữ đầy đủ quyền truy cập Speaking

**Files:** SpeakingService/ISpeakingService, SpeakingController, IndexVM/view,
actualfilterJS, query/role/integrationtests; Writingbatch theoPlangốc.

- [x] Red→green 25/25 via pages + topic filter finds tail-only topic.
- [x] True SQL filter/count/Skip/Take; Index query params; `/Speaking/Videos` Load more.
- [x] Playlist preview Take(6) retained; ownership/locked roles preserved in query.
- [ ] BE0 30-sample baseline / EXPLAIN MariaDB — still open.
- [ ] BE4 Writing SQL filters/aggregates — still open.

**Acceptance (local Speaking):** topic filter + paging reach full catalog/private.
Writing/EXPLAIN/perf lab not claimed.

## Tiếp tục các phần Plan2 còn thiếu

| Batch | Bằng chứng cần hoàn tất |
| --- | --- |
| BE0 | Data scale và30samplelab trước–sau, không chỉCountingInterceptorclass |
| BE5 | FakeSMTPstalled/fail/cancel/resultsemantics, unlockconsistency/idempotency; workerdefaultoff |
| BE6 | Profileawardedbadgesquery regression/noGETaward/userisolation vàquerymeasurement; launcherusage reuse cònthiếu |
| BE7 | PinexactMarked/SweetAlert/provenance/SRI/cascade, trace3samples, sanitizeddiagnostics/noXSS |
| BE8 | Fullfreshsuite, allnewregressions, MariaDBupgrade/repeat/preservation/concurrency/modelcheck, browserroles/mobile/full+embed; ASR/mic/cleanPC/hosting theoactualprereqs |

Externalgate chưa cóenvironment ghiunverified/blockercóchứngcứ, làmtiếpphần
localđộclập. Không bậtproduction/Plesk đểlấyacceptance. Không thaymodelweb hoặc
giới thiệuphoneme/semanticgrading ngoàiPlan2.

## Handoff

- [ ] Ledger từngD1–D3: red→fix→green,commands/artifacts,schemas/data risks,
  browser/provider/perf/skiplimits. Chỉbuglogfixđãverify, không blanketDONE.
- [ ] Fullsuitefresh0fail, newmeaningfultests, oldregressionsgiữ,Node5+expanded,
  EFpendingmodel, `git diff --check`; MariaDBrerun khiDockerready, không countskip.
- [ ] Dừng đểownerreview local, không commit/push/CI/merge/deploy. Gatefalse.

Prompt: [Cursor Auto](../../../.ai/prompts/cursor-plan2-third-review-repair.md).
