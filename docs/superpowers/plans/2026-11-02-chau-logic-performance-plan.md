# Plan2 — logic, hiệu năng và hạ tầng — Cursor Auto

Ngày02/11/2026. Cập nhật03/11: **ĐÃ ĐÓNG TRIỂN KHAI/KIỂM CHỨNG LOCAL BE0–BE8**.
[Báo cáo cuối](../../feature-audits/2026-11-03-plan1-plan2-local-closeout.md)
là trạng thái hiện hành; checklist dưới giữ làm thiết kế và tiêu chí gốc.
Mic người thật, clean-PC khác và staging/hosting là gate riêng chưa chạy,
được ghi rõ trong báo cáo. Không có quyền commit/push/merge/deploy từ trạng thái này.
Nhánh `feature/chau-ui-performance-audit`, baseline master210bf2d + diff Plan1
sau owner review. Một agent Cursor Auto thực thi tuần tự; không phụ thuộc model
Auto chọn. Không đổi allowlist IDE. Website giữ `gpt-6-luna`/reasoning `low`.

## 1. Phạm vi và quyền khi owner giao prompt thực thi

Sửa các finding L01–L11/U03/U06/U10 trong audit02/11; ưu tiên correctness trước
tối ưu. Không rewrite toàn bộ controllers hoặc thêm kiến trúc chung quá nhu cầu.
Giữ UI Plan1, routes cũ, JSON fields, SignalR events, ownership404, antiforgery,
Standard/Premium/Admin, learning receipt/credit/outbox và UTC+7 của Goals.
Quota AI đang dùng ngày UTC: giữ cho tới khi có quyết định nghiệp vụ riêng.

Cho phép DI registrations tối thiểu trong Program.cs và cấu hình **không chứa
secret** cần cho các bước dưới khi thực thi prompt. Không nâng target net9/EF9/
Pomelo9/package nền tảng, đổi key/model, thêm deps vào .csproj tùy tiện. Cho phép
additive EF migrations cần thiết cho reservation/index đã được chứng minh trong
BE2/BE4, kèm tct-ef-migration, metadata/snapshot và provider acceptance. Không
đổi applied migrations, migration history hay destructive schema/data cleanup.

Không push/commit/PR/remote CI/merge/deploy/Plesk/gate. Không gọi AI trả phí/gửi
mail thật cho automated regression. Không chia sẻ DB/key/backup hoặc native trust.
Không sửa file BTH2, source đồng đội ngoài caller bắt buộc hoặc diff Plan1 tùy ý.

Đọc AGENTS, [audit](../../feature-audits/2026-11-02-chau-ui-performance-audit.md),
ledger Plan1, tct-superpowers/tct-engineering và bug memory liên quan. API/admin/
speaking/security/EF skills chỉ nạp ở batch cần. CodeGraph source-first, không
full index. Microsoft EF references đã link ở audit; dùng đúng MariaDB provider.

## 2. Chuỗi thực thi

Handoff sau review Plan1: làm [preflightF1/F2](2026-11-02-plan2-handoff-preflight.md)
trước BE0; reviewer đã tái hiện5case cũ và xác nhận các fix hoạt động. Hai follow-up
nhỏ này không yêu cầu làm lại UI Plan1. Xem closeout02/11 cho phạm vi bằng chứng.

`BE0 → BE1 → BE2 → BE3 → BE4 → BE5 → BE6 → BE7 → BE8`.
BE1/BE2 là correctness gate; không gọi toàn Plan2 hoàn tất nếu các mục này còn
lỗi đã tái hiện. BE3/BE4 dùng seam đo củaBE0; BE8 kiểm lại toàn bộ tích hợp.
Chạy từng batch nhỏ; một failure phải diagnose rồi mới chuyển việc phụ thuộc.

Ledger: `docs/feature-audits/2026-11-02-chau-logic-performance-ledger.md`.
Ghi baseline/tree/commands/red-green/provider/samples/diff/risks/blockers; mỗi
batch là checkpoint. Nếu hết context tiếp tục batch dở, không làm lại toàn bộ.

## BE0 — baseline, contracts và cơ chế đo (P0 quy trình)

- [ ] Kiểm branch/dirty diff; lập manifest source Plan1 phải giữ. Xác nhận fixes
  đã có trong master, không mở lại findings cũ chỉ vì checklist lịch sử chưa tick.
- [ ] Local launcher chuẩn; busy port giữ workload, chọn free port Release.
  Không kill PID theo tên. Xác nhận health/speech/key-source metadata.
- [ ] Lập fixture disposable MariaDB10.5.23 đúng bootstrap/sentinel/runId/port;
  script có identity guard hiện hành, đọc parameters thật trước dùng. Không reset
  tctenglish_dev, dùng production DB hoặc hardcode password vào argv/repo.
- [ ] Seed synthetic có ownership: Standard/Premium/Admin +outsider; catalog
  200 và2000 records, Writing attempts theo exercise, Chat history50/500/5000.
  Kích thước là mục tiêu thí nghiệm, tạo trong DB riêng; không copy hosting users.
- [ ] Dùng DbCommandInterceptor/test-owned diagnostics đếm command và rows/payload
  trên workflow; logs không chứa prompt/password/token/email thật. Không dùng
  EnableSensitiveDataLogging hoặc global SQL logging trong production.
- [ ] Đo Release:5 warm-up +ít nhất30 samples/page/profile/query kịch bản nặng,
  ghi hardware/processes/data/concurrency/cache, median/p95/max.30 mẫu chỉ là
  thí nghiệm lab; so với cùng fixture/config, không suy ra hosting SLA.
- [ ] Đo hiện trạng trước sửa, không coi số nhỏ HTTP loopback trong audit là
  performance acceptance. Mỗi tối ưu phải giảm data/command/work có ý nghĩa.

**Gate:** reproducible scripts + sanitized baseline, không paid/provider dependency.
Không thêm production profiler hoặc full-build trong mỗi hook.

## BE1 — Account correctness và trạng thái lỗi (L01,U06,U10)

**Files:** Controllers/AccountController.cs; account/security slice service nhỏ
nếu cần; typed ViewModels hiện có; Account tests mới; DI tối thiểu. Không làm
service extraction cả controller >1000 dòng trong một lượt.

- [ ] Red HTTP regression: social sentinel/malformed bcrypt/null-equivalent
  legacy data ở fixture không gây500 khi login local. Guard nhận dạng hash trước
  verify; generic failure không enumerate account; không auto-link/autoset password.
- [ ] Kiểm Register/Reset/UpdateEmail/ChangePassword validation phía server;
  yêu cầu field/format/length có trong ViewModels/UI phải được kiểm trước BCrypt/
  SaveChanges. Không chỉ kiểm password ở client. Giữ existing valid login/reset.
- [ ] Reset token hợp lệ/hết hạn/null expiry/reuse/2 lần submit đồng thời: một
  reset hợp lệ được consume an toàn; không chấp nhận expiry thiếu; không mất
  email generic response. Dùng fake email sender, không gửi email thật.
- [ ] Settings provider flags: prefix không đủ để xác nhận provider liên kết;
  hiển thị unknown/khả năng đã biết đúng source. Không tự tạo OAuth identity schema.
- [ ] Standard GET Admin hiện bị chuyển tới /Account/AccessDenied không có trang
  và404. Thêm handler/view403 có lời giải thích/đường quay lại an toàn; không đổi
  IDOR404 của feature endpoints thành403. Anonymous vẫn challenge như contract.
- [ ] Chuẩn hóa text response tiếng Việt đang mojibake trong UserManagement
  khi đã chứng minh lỗi; không đổi response shape/status theo tiện tay.

**Tests:** synthetic local+OAuth principal fake; valid/invalid ModelState;
antiforgery rejection, anonymous, self/outsider, reset replay, persisted data
unchanged on invalid. BCrypt probe trong audit chỉ chứng minh library exception;
Cursor phải thêm HTTP red trước sửa để chứng minh endpoint.

## BE2 — Chat cancel, stop, quota và gửi lặp (L02,L03,L11)

**Files:** Services/AI/{AiChatService,AiStreamingService,AiConversationExecutionGuard,
IAiStreamingService,...}; Controllers/AiController; Hubs/ClassChatHub chỉ AI
methods; Realtime/AiStreamMessages; JS ai-chat.js phần transport; tests AI hiện
có và regression mới; new narrow reservation model/service nếu red yêu cầu.

- [ ] Red caller-cancel sau user-message save: conversation mới và cũ. Không
  assistant success/log thành công giả, không dangling prompt/draft, lease được
  nhả, lượt sau chạy đúng. Cleanup token riêng bounded khi request ct đã cancel;
  giữ original exception, không xóa message của lượt khác/đã committed reply.
- [ ] Kiểm cả HTTP fallback và SignalR đang dùng. Server hiện await provider
  trước AiStreamStarted; client Stop return nếu chưa có activeStreamId. Viết test
  controllable fake provider đang chờ, bấm Stop và assert provider được cancel
  trước trả output. Kiểm scheduling Hub để Stop không bị xếp sau Start dài.
- [ ] Thiết kế request identity/cancel handle biết trước provider; giữ tương
  thích StartAiStream/StopAiStream và AiStream* events cũ hoặc thêm method/field
  mới có adapter. Handle gắn user+connection+request, không cancel tab/user khác.
  Không Task.Run/fire-and-forget untracked hoặc thay đổi Class chat nghiệp vụ.
- [ ] Loss/reconnect/Stop trước Started/sau Started/sau commit: không tự gửi lại
  HTTP cùng intent sau server đã xử lý. Fingerprint/requestId cùng intent xuyên
  transport nếu bổ sung; same id/same payload trả trạng thái/kết quả đã biết,
  changed payload409, outsider404. Trạng thái không biết rõ: trả lỗi có thể phục
  hồi, không hứa exactly-once remote hoặc tự gọi lại và tính phí lần nữa.
- [ ] Red quota barrier: seed14 successful requests hôm nay;2 conversations của
  cùng Standard cùng qua count trước khi provider kết thúc. Expected chỉ1 lượt
  được phép dùng slot15. Daily tokens tương tự. Fake provider,2 scopes/contexts;
  MariaDB thật cho concurrency, không chứng minh bằng SQLite alone.
- [ ] Nếu tái hiện, dùng DB-authoritative reservation theo user/ngày +requestId,
  không chỉ static semaphore per-process. Viết design ngắn trong ledger trước
  migration: trạng thái pending/settled/released/unknown; pending chiếm slot và
  token dự trù; reserve/finalize/release atomic, repeat không cộng/nhả2lần.
- [ ] Count historical successful logs vẫn đúng; không reset quota khi nâng cấp.
  Transaction ngắn reserve/settle, KHÔNG giữ transaction/row lock qua HTTP OpenAI.
  Failure trước gửi được nhả; provider outcome không rõ xử lý conservative và
  có reconciliation/expiry được kiểm, không hoàn token giả. Giữ UTC day boundary
  của AI; admin/premium policy hiện hành vẫn đúng.
- [ ] Additive schema nếu cần: nullable/backfill có quy tắc, unique request key,
  FK/index đúng thứ tự, Designer+snapshot trong diff. Repo-local EF, migrate/repeat
  và bảo toàn lịch sử trên disposable MariaDB; không tự sửa old migrations.

**Gate tests:** red→green cancel/new/existing, Stop in-flight, disconnect/fallback
single intent, concurrent quota same/different users, dedup/replay/error/expiry,
process restart và2 service instances, all2transports và owner checks. Không
khẳng định remote billing exactly-once nếu chỉ kiểm application dedup.

## BE3 — Chat history và context có giới hạn (L06)

**Files:** AiConversationService/interfaces, AiChatService, AiContextBuilder,
AiController, AI ViewModels, Chat/_ChatShell, ai-chat.js integration nhỏ.

- [ ] Mặc định history list30 conversations, message window50; cursor stable
  `(UpdatedAtUtc,Id)` / `(CreatedAtUtc,Id)`, pageSize clamp1–100; opaque cursor
  validate malformed. Đây là đề xuất contract mới cần ghi rõ trong ledger.
- [ ] Load older/More thực sự trên server, preserve chronological order, focus
  và scroll position. Không lấy5000 rows rồi Take ở memory; không xóa history cũ.
- [ ] Request provider context: đọc các batch newest bounded đến token budget,
  chỉ project fields cần; giữ role/system/current-message ordering và budget4600
  hiện hành. Không arbitrary Take làm thay ngữ nghĩa một cách im lặng; ghi rõ cap
  nếu thêm max-history policy; tests dài/context boundary và stable tie ordering.
- [ ] Auth/ownership ở mọi page/cursor/query; IDOR404; không tin userId từ client.
  Giữ existing routes +fields; thêm metadata/endpoints thay vì đổi envelope đột ngột.
- [ ] Cập nhật consumer markup/JS tối thiểu trên UI Plan1, không reformat/redesign
  composer. Không nhầm HTTP và SignalR thành hai lịch sử riêng.

**Gate:**5000 messages không tạo5000 message DOM hoặc full DB list ở lượt đầu;
load older đủ, không duplicate/gap khi timestamp trùng; prompt budget/AI tests pass.

## BE4 — Speaking/Writing catalog và progress queries (L04,L05)

**Files:** SpeakingService/interfaces/index ViewModel/controller;
WritingService/interfaces/ViewModels/StudyController; JS speaking/writing-exercises
phần query/page; Admin content list nếu BE0 đo xác nhận unbounded; DbContext/
migrations chỉ index có EXPLAIN chứng minh.

- [ ] Speaking: thiết kế paged public/my lists default24/max100, stable sort
  thêmId, level/topic/query/page phía server; playlists preview tối đa6 video và
  link xem đủ. Không render duplicate full catalog trong mỗi nhóm. Không cắt
  private/locked content mà bỏ dấu hiệu quyền Standard/Premium.
- [ ] Writing: page12 hiện tại giữ semantics filters/topic/status/my exercises;
  apply SQL filters/count/page trước materialize. Status filters phải dựa aggregate
  đúng toàn bộ attempt, không dựa chỉ5 attempts mới nhất rồi đánh giá sai progress.
- [ ] Metadata list: chỉ query count/latest/passed-per-sentence/lastAttempt;
  recent feedback detail chỉ khi practice/history cần. Group dictionary lookup,
  tránh `exercises.First` và `attemptRows.Where` lặp theo toàn catalog.
- [ ] Practice resume cần đầy đủ trạng thái từng câu; aggregate latest đúng,
  giữ history endpoint để xem đủ attempt. Không xóa attempts để làm nhẹ query.
- [ ] SpeakingPractice hiện filtered Include progress đúng user. Giữ ownership
  và behavior này; payload mẫu231064bytes là một case, đo sentence/DOM duplication
  trước đề xuất lazy transcript. Không cắt sentence khiến completion/resume sai.
- [ ] Admin user list đã paging20 và Dashboard aggregates; không viết lại vì
  backlog chung nói chưa paging. Admin Speaking/Writing lists: đo/đọc Index thật,
  thêm paging/filter/projection nếu còn full loads và chứng minh lợi ích.
- [ ] Lấy SQL/EXPLAIN trên MariaDB10.5.23 cho slow query. Index hiện có đã gồm
  owner/created, WritingAttempts user/exercise/time và user/sentence/time; không
  thêm duplicate indexes hoặc sao chép recommendation SQL Server cũ.
- [ ] API backward compatibility: giữ URL/query hiện có; nếu đổi sang paged
  payload, cập nhật tất cả consumers cùng batch và tests JSON/access. Không giả
  bảo toàn contract bằng giữ field nhưng cắt dữ liệu khiến client filter thiếu.

**Gate:** catalog2000 không load2000 cards/attempt feedback vào list page;
query count không tăng N+1; SQL LIMIT thực; no cross-user/role data; public/private,
filter/page/status/resume đều đúng; relative payload/allocations/latency có evidence.

## BE5 — SMTP và unlock vận hành an toàn (L08)

**Files:** IAppEmailSender/SmtpAppEmailSender, Account/Admin callers,
AutoUnlockWorker, fake SMTP transport tests; DI/settings không secret nếu cần.

- [ ] Tạo seam fake transport để đo stall/failure/cancel. Deadline hữu hạn
  theo workflow và linked cancellation cho SendMailAsync đúng .NET9 overload;
  verify official docs trước dùng. Không dùng .Result/Wait hoặc Task.Run bọc I/O.
- [ ] Trả delivery result rõ ở nội bộ thay vì Task bỏ bool; Forgot response
  generic chống enumeration vẫn giữ; block/unlock business commit không bị
  báo là thất bại hoàn toàn khi chỉ email lỗi. Log structured, không reset link/key.
- [ ] Worker: state transition/claim và mail gửi có thứ tự/idempotency được test;
  cancellation phải thoát loop đúng, failure không làm skip hàng loạt còn lại.
  Không hứa exactly-once email khi provider không hỗ trợ.
- [ ] Tránh làm email outbox lớn trong đợt này. Nếu cần durable retry mới để đạt
  yêu cầu nghiệp vụ, ghi design/scope riêng; không reuse LearningNotificationOutbox
  sai event contract hoặc mở DB transaction qua SMTP. Không chọn silent drop.
- [ ] Test LockExpiry null/permanent/expired, lazy-login/admin/worker consistency;
  enable chỉ trong test host/disposable local theo cấu hình. Giữ production/default
  disabled tới khi có rollout dữ liệu và owner cho phép riêng.
- [ ] HtmlEncode các giá trị đưa vào email HTML, giữ reset URL được xây an toàn;
  không ghi recipient/payload nhạy cảm vào benchmark artifact chia sẻ.

**Gate:** fake stalled SMTP bounded và cancel, failed send quan sát được, locked
data transition đúng; không live email, không tự bật worker cho hosting.

## BE6 — Profile/Goals và truy vấn render chung (L07,U03,U06)

- [ ] Đếm trước: Profile đang gọi full GetGoalsAsync chỉ lấy badges. Tạo query
  badges/profile summary riêng nếu có lợi; không refresh/award badges trên GET.
- [ ] Nếu thêm mini-progress thật: typed VM theo đúng active goal target/today
  business date UTC+7, zero/missing có label; không suy từ số bài toàn hệ thống.
  UI Plan1 CTA là fallback hợp lệ, không bắt buộc thêm widget mới.
- [ ] _AiChatLauncher gọi daily usage mỗi view; dùng request-scoped reuse hoặc
  lazy fetch có trạng thái honest nếu số query ảnh hưởng thực. Không static cache
  theo userId thiếu expiry/invalidation; không shared output cache private HTML.
- [ ] Goals five areas/credit/outbox đảm bảo tests hiện có. Không gộp COUNT async
  bằng Task.WhenAll trên cùng scoped DbContext. Profile improvement không đổi XP.
- [ ] Nếu extract service Account/Admin, làm đúng workflow đã chạm; không generic
  repository hoặc refactor không chứng minh lợi ích. Cập nhật known issue có chứng
  cứ, không đánh dấu toàn TD-002/003 đã xong khi chỉ tách một method.

**Gate:** Profile data/user đúng, existing goal/reward regressions pass; giảm query
hoặc payload có số liệu, không chỉ đổi tên method.

## BE7 — assets, deadline và chẩn đoán (L09)

- [ ] Pin exact bản Marked/SweetAlert2 đang dùng đã kiểm API/license/hash thay vì
  latest/@11 floating. Ưu tiên vendored được phép hoặc exact CDN +SRI phù hợp;
  ghi provenance, không tự chọn version từ trí nhớ. Không nâng major vì tiện.
- [ ] Bỏ CSS @import duplicate sau kiểm cascade order, dependency fonts/icons;
  defer script không cần lúc first paint khi không phá inline initialization.
  Version local CSS bằng asp-append-version. Không thêm minifier/build chain mới.
- [ ] Kiểm render blocking bằng trace3 mẫu cùng điều kiện; giữ brand/fonts, không
  hứa savings theo số estimated của DevTools. Không đặt cache public cho pages
  có login/CSRF/role/content riêng. Compression chỉ sau đo/config đúng môi trường,
  không bật tùy tiện trên responses chứa secret.
- [ ] Expose sanitized request duration/error category/correlation, queue wait
  và inference duration nếu thiếu để debug; không log audio/prompt/API key hoặc
  đường dẫn secrets. Readiness app != speech != cloud quota, UI thông điệp đúng.

**Gate:** no missing assets/console errors/Markdown XSS; request count/payload/trace
so trước-sau; offline/failing CDN graceful theo tính năng hỗ trợ, không tuyên bố
YouTube/AI đều offline. Tooling changes nếu có phải chạy required checks AGENTS.

## BE8 — acceptance và handoff (L10)

- [ ] Build +focused tests mỗi logic batch; new bugs có red regression trước fix.
  Cuối cùng `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release`
  với cloud flags off. Không đổi tests để bỏ guard hoặc giấu platform failure.
- [ ] Provider tests mới/đổi: disposable MariaDB đúng identity, upgrade+repeat,
  preservation; model changes → repo-local EF pending-model check không còn drift.
  Logs sanitize, scripts command thật ghi ledger; không tự invent sentinel/history.
- [ ] Chạy Speaking realASR opt-in trên loopback được pin khi có engine; mic người
  thật/accent/noise và load đồng thời ghi manual/unverified nếu không thực hiện.
  Giữ inference15s/queue caps; không tăng threads/model/timeouts để che failure.
- [ ] Browser authenticated Standard/Premium/Admin: Account/Goals/Speaking/Writing/
  AI full+embed/Admin; verify Plan1 selectors/mobile/accessibility không regress.
  Automated AI dùng fake provider; live paid chỉ theo owner authority/budget cụ thể.
- [ ] Clean teammate setup: chạy doctor/setup trên máy Windows khác nếu có; nếu
  chỉ disposable DB và empty directory thì ghi đúng giới hạn, không gọi clean-PC pass.
- [ ] Ghi topology cho N518: IIS app/MariaDB/ASR/FFmpeg chạy ở đâu, quyền/service
  manager/network TLS/queue/auth/CPU-RAM cần nhà cung cấp xác nhận. Docker Desktop
  local không chứng minh gói shared hosting hỗ trợ Docker hoặc whisper-server.
- [ ] Staging release/failure/restore là gate riêng: không bật production để lấy
  evidence. Chỉ update runbook/preflight local khi được scope này cho phép; enabled
  deploy drill cần môi trường và authority cụ thể riêng, ghi deferred nếu thiếu.
- [ ] `git diff --check`, review diff theo ownership/contracts; cập nhật bug-fix-log
  cho bugs thực sửa và known-issues đúng phần. Báo actual build/tests/pass/skip,
  migration risk/perf tradeoff/browser limits; không blanket 'hoàn tất mọi thứ'.
- [ ] Dừng local để owner review. Không commit/push/PR/CI/merge hoặc deploy.

## 3. Definition of done theo bằng chứng

| Gate | Chứng cứ bắt buộc |
| --- | --- |
| Account |no500 social hash; server validation; reset consume;403 page vsIDOR404|
| Chat correctness |cancel/Stop before provider end; no duplicate intent/fallback; quota races provider-real proof|
| Query scaling |SQL paging/projection; history load older; fixture scale, actual rows/bytes/queries before-after|
| Learning |receipt/credit/replay/resume/5 areas +ownership regressions không đổi|
| SMTP/unlock |fake failure/cancel/deadline, state consistency, worker mặc định không tự bật|
| Local infra |doctor+health+ASR+key-source; external prerequisites/prod topology ghi đúng giới hạn|
| Handoff |Plan1 preserved, schema complete, checks evidence, explicit remaining acceptance, no remote stage|

Pronunciation phoneme/prosody và chấm ngữ nghĩa Writing mở rộng là đề tài sản
phẩm riêng. Plan2 tối ưu vận hành/correctness hiện có, không hứa thêm hai engine này.
