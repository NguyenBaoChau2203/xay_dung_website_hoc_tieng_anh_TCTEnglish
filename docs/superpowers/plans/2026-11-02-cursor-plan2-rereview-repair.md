# Plan sửa review vòng2 và hoàn tất Plan2

02/11/2026. **PLANNED**, thực thi khi owner giao prompt. Một agent inline,
nhánh`feature/chau-ui-performance-audit`, giữ UI Plan1/dữ liệu/BTH2 và dirty work.

Đọc [review vòng2](../../feature-audits/2026-11-02-cursor-plan2-rereview.md),
[plan repair trước](2026-11-02-cursor-plan2-review-repair.md) và active batch trong
[Plan2 gốc](2026-11-02-chau-logic-performance-plan.md). Vòng trước9probe nay pass;
không mở lại UI Plan1 toàn bộ, không xem đó là bằng chứng mọi invariant đã đúng.

## Quyền khi thực thi và thứ tự

Giữ quyền có giới hạn Plan2: source/services/controllers/VM/tests/JS cần thiết;
Program chỉ DI/flags không secret đúng scope. Additive schema phải qua EF skill,
Designer/snapshot, upgrade/repeat/preservation MariaDB10.5.23 disposable và model
check. Không edit applied migration/history, DROP/reset devDB, package upgrade
tùy tiện hoặc hardcode secrets. Website Luna/low, ASRWhisper; model IDE tách riêng.

Chỉ local, không commit/push/PR/CIremote/merge/deploy/Plesk; gatefalse. Fake
provider/SMTP, không paid/live API. Không subagent/CLI AI/chat khác. Preserve
listeners/workloads; nếu cần runlocal dùng launcher chuẩn và free port.

`RR0 → RR1(C1,C5) → RR2(C2,C3) → RR3(C4) → RR4(C6) → BE0 còn thiếu → BE4–BE8`.
Mỗi batch red→fix→green + ledger. Không dừng ở4tests mới xanh khi các batch còn
thiếu vẫn chưa được thực hiện. External gate không có prerequisite ghi đúng
unverified và tiếp tục phần local độc lập; không deploy để lấy chứng cứ.

## RR0 — Snapshot, evidence và fixture

- [ ] Branch/status/manifest, không reset/stash/reformat toàn UI.
- [ ] Ledger sửa nhãn DONE RP0–RP6 thành closed theo từng trigger/invariant;
  giữ lịch sử838/0/10/848 và9probe cũ pass, không xóa kết quả cũ.
- [ ] Tham khảo `.tmp/cursor-repair-review/RepairReviewTests.cs` và TRX, tạo
  regression chính thức phù hợp. Không compile đồng thời import tạm và bản chính.
  Artifact ignored thiếu ở máy khác: dựng lại đúng trigger từ report.
- [ ] Disposable MariaDB đúng host/name/port/runId/sentinel trước DML. Container
  p2rp1102a/3328 hiện có là fixture riêng, không phải tctenglish_dev. Không đưa
  connection/password lên chat/log/argv hoặc làm yếu guard để chạy test.

## RR1 — C1/C5: quota transaction đúng khi cạnh tranh và không phá caller

**Files:** AiDailyQuotaReservationService/model interfaces; AiChatService;
quota tests; Program DI tối thiểu nếu design cần context riêng.

- [ ] Red MariaDB: seed13settled/còn2slots, barrier sau SELECT để hai contexts
  cùng thấy state; reserve đồng thời phải thành công2, pending2, không exception
  deadlock lọt ra HTTP500. Fixture vòng2 hiện1success/1deadlock.
- [ ] Thêm first-row creation race; seed14/còn1slot →1reserve+1rate-limit theo
  contract; same/different users, tokens, settle/release cạnh tranh, replay.
- [ ] Chọn atomic mutation/locking/concurrency policy thật phù hợp MariaDB.
  Serializable read rồi update có thể deadlock; retry bounded có deadline/fresh
  state. Retry identity giữ ổn định qua attempt, không sinh logical request mới
  mỗi lần; commit ambiguity có verify, không double reserve/settle.
- [ ] Không bật global EnableRetryOnFailure rồi claim đãxong nếu mọi transaction
  khác chưa hỗ trợ replay. Không static processlock thay DBauthority. Nếu dùng
  provider errors1213/1205/1062 phải phân loại đúng, không retry mọi exception.
- [ ] Loại bỏ Clear toàn DbContext dùng chung. Context riêng hoặc reload đúng
  entries/atomic query phải preserve các entity và thay đổi đang pending của
  caller. Không chữa bằng reattach toàn graph mù quáng.
- [ ] Red persisted recency: existing conversation3ngàytrước, sendfake thành
  công →UpdatedAt mới và reload history lên đầu; đủ user+assistant messages.
- [ ] Giữ counters/replay/historical tests vòng1, backend ownership và noDB
  transaction qua HTTP. Schema nếu thay phải đủ EF artifacts và provider proof.

**Gate:** old9probes pass; new simultaneousMariaDB cases0fail; conversation
recency persisted đúng; không side effect với tracking của caller.

## RR2 — C2/C3: committed reply/finalization/cancel/retry cùng một lifecycle

**Files:** AiChatService/quota ledger; AiController/send contract; streaming/Hub
AI slice; ai-chat.js transport; cancellation/failure/recovery tests.

- [ ] Red: provider reply+assistant+successlog đãcommit, cancel khi Settle bắt
  đầu →quota không được Released0/0. Giữ reply và knownusage.
- [ ] Red: inject finalize exception saucommit, existingconversation HTTP500;
  retry sameintent không được provider2/assistant2. Phải kiểm cả productionJS
  retry/fallback, không chỉ service hoặc một POST thủ công.
- [ ] Định nghĩa lifecycle trướcdispatch/dispatching/knownreply/replycommitted/
  finalized/unknown. Failure trước gửi có thể release; provider outcome chưa
  biết không được mặc định coi chưa tính phí. Không xóa reply để giả rollback.
- [ ] Finalize/cleanup bounded độc lập caller abort khi kết quả đãbiết; preserve
  exception thực, lease và saved data. Noncancel DBfailure/crash có persisted
  recovery/reconciliation, pending không treo vĩnh viễn hoặc bị release mù.
- [ ] Tối thiểu bỏ tự retry khi không biết server đã xử lý hay chưa; nếu thêm
  intent/requestId dùng xuyên HTTP/SignalR với user/payload binding, stable qua
  retry và known-result/status. Sameid changedpayload409, outsider404; routes và
  fields cũ có adapter. Guid quota sinh mới mỗi attempt không phải intent dedup.
- [ ] Kiểm Stop trướcStarted/đangprovider/saucommit, disconnect, loss response,
  retry500/503, settlementfail và processrestart. Không claim remote exactly-once
  billing. Không Task.Run/fireforget; không đồng thời EF trên cùng context.
- [ ] Bao phủ mutation đầu tiên (create/prompt save) đếnterminal state; không
  đặt try chỉ sau Save rồi bỏ transaction/outcome ambiguity ngoài phạm vi.

**Gate:** postcommit cancel giữ đúng quota, retries cùng intent chỉ1provider
trong trường hợp đãbiết xử lý; unknown outcome không bị tự gửi mới, recovery
được kiểm. Legacy HTTP/SignalR và9probes cũ không regress.

## RR3 — C4: Load older đúng thứ tự và có kiểm thử hành vi

**Files:** ai-chat.js loadOlderMessages/loadOlderConversations; relevant
Razor/VM/API integration; JS/browser/endpoint regression tests.

- [ ] Đọc DESIGN.md và UI component/review procedure, giữ UIPlan1.
- [ ] Red production function/DOM: existingm4,m5, APIm0,m1,m2,m3 →DOM phải
  m0,m1,m2,m3,m4,m5; hiệnm3,m2,m1,m0,m4,m5.
- [ ] Insert fragment hoặc reference ổn định giữ page chronological. Load2–3
  pages, tie timestamps+id, initialSSR50 và150+messages; no duplicate/gap.
- [ ] Preserve scroll offset/focus khi thêmrows, hide/removebutton đúng hết
  dữ liệu; network401/404/500/invalidpayload có error/retryfeedback, không silent.
- [ ] Giữ Markdown sanitize, role/avatar conventions; loadedconversation có
  actiondelete theocontract, active title/querystring/backforward đúng. Không chỉ
  test string códata-ai-load-older là đủ acceptance.
- [ ] Cursor malformed/partial, ownership404, bounds100; context/history query
  theo BE3 tokenbudget hoặc policycap rõ, không declareBE3done khi chỉ cóbutton.

**Gate:** actualDOM full+embed chronological, multipage/ties/scroll và delete
đúng; relevantJS/security/querytests pass, không full-history materialization.

## RR4 — C6 và phần RP5 còn thiếu: server validation/reset consume

**Files:** AccountController/narrow input VMs/services; Account tests.

- [ ] Red auth+CSRF UpdateEmail`not-an-email`: reject không persist, email cũ
  và cookieidentity hợp lệ. Case valid change vẫn success.
- [ ] Reuse validator theo workflow cho format/length/normalization/uniqueness;
  match schema/UI contract. Không ModelState toàn sharedVM yêu cầu nhập mật khẩu
  chỉ để updateemail. Invalidinput phải bị chặn trướchash/write.
- [ ] Giữ Registerinvalid/ChangePasswordshort+badstoredhash/localvalid/OAuth
  sentinel tests đãxanh. Không auto-link OAuth hoặc bypass currentpassword.
- [ ] Reset valid/expired/null/reuse/concurrent2POST: đúng1consume thànhcông,
  request còn lại không overwrite mật khẩu. Dùng barrier/provider phù hợp,
  không test tuần tự rồi gọi đó là concurrency proof. Không liveSMTP/enumeration.

**Gate:** valid/invalidAccountworkflow tests + concurrenttoken proof xanh,
403Admin/404IDOR/antiforgery giữ nguyên; BE1 mới được đóng theođầyđủchecklist.

## Tiếp tục phần Plan2 còn thiếu

Theo checklist gốc và repair vòng1, hoàn tất từng batch sau correctness:

| Batch | Chứng cứ cần có |
| --- | --- |
| BE0 | Synthetic200/2000catalog,50/500/5000history; warmup5+30samples; actualqueryrows/count/bytes/allocations/latency baseline trước tối ưu |
| BE4 | SQL filters/count/page/aggregates SpeakingWriting, EXPLAINMariaDB; preserve resume/history/private/role/receiptcredits; không cắtdata mất UIsemantics |
| BE5 | FakeSMTP stalled/fail/cancel deadline, explicitresults/HTMLencode/sanitizedlogs; unlockconsistency/idempotency; workerdefaultoff |
| BE6 | Profilebadgesquery/launcherusage đo trước–sau; noGETawards/sharedprivatecache/parallelEFsamecontext |
| BE7 | Pinexactassets/provenance/SRI/cascade, trace3samples, noMarkdownXSS/consoleerrors, sanitizeddiagnostics |
| BE8 | Freshfullsuite0fail, newregressions, MariaDBmigrateupgrade/repeat/preservation+concurrency, browserroles/mobile/full+embed; ASR/mic/cleanPC/hosting đúngprerequisites/limits |

Không dùng Dockerlocal chứng minh hostingN518 cóDocker/ASRdaemon. Stagingrelease/
restore là gate riêng có quyền riêng; không mở production trongscope này.

## Handoff

- [ ] Ledger C1–C6 theo red→fix→green/commands/artifacts, update knownissues và
  buglog chỉ khi fix thậtđãverify. Ghi mỗi BE thực hiện gì/chưa làm gì.
- [ ] Fullsuite fresh không `--no-build` stale; configriêng khiDLLbusy được phép,
  ghi actualcounters/flags. 10skip không phải10pass; MariaDBruns táchriêng.
- [ ] Node4submitguard +newpaging/transporttests, EFpendingmodel, `git diff --check`;
  tooling checks chỉ khicótoolingchange, không thay applicationacceptance.
- [ ] Preserve dirtywork; báo owner review, không commit/push/CI/merge/deploy.

Prompt: [Cursor Auto](../../../.ai/prompts/cursor-plan2-rereview-repair.md).
