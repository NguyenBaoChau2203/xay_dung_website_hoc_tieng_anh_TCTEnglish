# Review Cursor Plan 2 — 02/11/2026

**Kết luận: chưa nghiệm thu Plan 2.** Cursor đã làm preflight và một phần
BE1–BE3/BE5, chưa làm hết BE0–BE8. Các regression mới hiện pass nhưng full suite
đang đỏ; review tái hiện thêm lỗi ở phần đã đánh dấu hoàn tất.

- Nhánh: `feature/chau-ui-performance-audit`; HEAD `210bf2d`.
- Phạm vi review: diff Cursor so với baseline + UI Plan 1 đang dirty được giữ lại.
- Một agent điều tra/review/kiểm chứng; không sửa runtime, commit/push/CI/PR/merge.
- Đã đọc gate GitHub: `PLESK_DEPLOYMENT_ENABLED=false`. Không deploy hay đổi gate.
- Website vẫn `gpt-6-luna` / `low`; các probe dùng provider giả, không gọi API trả phí.

## Bằng chứng kiểm thử mới của reviewer

| Kiểm tra | Kết quả | Giới hạn |
| --- | --- | --- |
| Fresh full application suite, configuration `CursorPlan2Review`, Optimize=true | **816 pass / 13 fail / 8 skipped / 837**; 3m51s | Không dùng DLL cũ hoặc `--no-build`; không phải production acceptance |
| `node --test scripts/tests/test_ai_chat_submit_guard.cjs` | **4/4 pass** | Thực thi JS production, fake DOM/fetch; không chứng minh SignalR Stop |
| 9 probe bổ sung của reviewer | **9 fail / 9**, tái hiện các finding bên dưới | Dùng SQLite/TestServer/provider giả; đây là regression còn đỏ, không phải 9 test pass |
| EF `has-pending-model-changes`, build mới `CursorPlan2Review` | Sạch | Chỉ chứng minh model/snapshot khớp; không chứng minh migration đã chạy trên MariaDB |
| `git diff --check` | Pass | LF/CRLF warnings không phải lỗi whitespace |
| MariaDB migration upgrade/repeat/preservation và quota concurrency | **Chưa chạy** | Cursor cũng ghi chưa chạy; chưa thể chấp nhận schema/quota theo provider hosting |

Full suite có 18 case mới của Cursor, tất cả pass; 13 failure hiện tại gồm
12 test `AiChatServiceTests` và 1 test hardening về quota. Không sửa assertion hay
bỏ test để che lỗi. 8 skipped là các acceptance cần opt-in/prerequisite; không tính pass.

Artifacts riêng, ignored:

- `.tmp/cursor-plan2-review/cursor-plan2-review.trx` — full suite.
- `.tmp/cursor-plan2-review/review-probes.trx` — 9 probe tái hiện.
- `.tmp/cursor-plan2-review/ReviewProbeTests.cs` + `ReviewProbe.targets` — mã probe.
- `.tmp/cursor-plan2-review/probe-run.log` — log build/chạy probe.

Lệnh tái chạy probe tại checkout này:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c CursorPlan2Probe -p:Optimize=true -p:CustomAfterMicrosoftCommonTargets=D:/xay_dung_website_hoc_tieng_anh_TCTEnglish/.tmp/cursor-plan2-review/ReviewProbe.targets --filter 'FullyQualifiedName~CursorReviewProbes' --logger 'trx;LogFileName=review-probes.trx' --results-directory .tmp/cursor-plan2-review --verbosity quiet
```

File `.targets` chỉ đưa probe vào test project khi lệnh trên chỉ định; không đổi
`.csproj` hay source ứng dụng. Khi đưa regression vào bộ test chính thức, bỏ cơ
chế import tạm, không compile một test hai lần. Artifact ignored không đi theo Git.

## Findings theo ưu tiên

### R1 — P1: quota mất cập nhật giữa các DbContext, finalize chưa idempotent

**Files:** `Services/AI/AiDailyQuotaReservationService.cs:39–82,86–124`;
`Models/AiUserDailyQuota.cs`; caller `AiChatService.cs:297–303`.

Hai scope A/B cùng user/ngày reserve trước khi hai provider kết thúc. Mỗi scope
giữ entity quota đã tracking. Transaction mới và SELECT lại trong `MutateAsync`
không refresh giá trị entity đang tracking. A settle rồi B settle có thể ghi đè
counters của nhau.

Probe `Quota_OverlappingScopes_DoNotLoseSettledOrPendingCounts` cho kết quả thật:
**SettledQuestions=1, PendingQuestions=1, SettledTokens=20, PendingTokens=100**,
thay vì **2/0/40/0** sau hai lượt đã hoàn tất. Có transaction Serializable vẫn sai.

Probe `Quota_FinalizationReplayDoesNotDoubleCount` settle cùng reservation hai
lần cho **2 settled / 40 tokens**, thay vì **1 / 20**. Reservation chỉ gồm
user/ngày/số token, không có request identity hoặc persisted terminal state.
Không thể phân biệt replay với request khác; process crash cũng không có cơ chế
đã kiểm chứng để giải quyết pending/unknown.

**Sửa:** reservation theo request identity, state transition atomic/idempotent,
không dùng entity stale để ghi counter tuyệt đối; kiểm nhiều scope/instance,
settle/release/replay/unknown/recovery trên MariaDB disposable. Không giữ DB
transaction qua provider HTTP. Test quota mới của Cursor chỉ chạy B sau khi A
đã reserve xong và chỉ còn một slot; không kiểm hai reservation cùng sống rồi finalize.

### R2 — P1: chuyển sang bảng quota mới bỏ qua lịch sử dùng trong ngày

**Files:** `AiDailyQuotaReservationService.cs:45–58`;
`Migrations/20261102104017_AddAiUserDailyQuota.cs:14–46`;
`AiChatService.cs:445–468`.

Migration tạo bảng trống; khi chưa có row, service khởi tạo settled=0, không đọc
success logs hiện có. UI usage vẫn đếm `AiRequestLogs`, nên UI và enforcement
dùng hai nguồn không đồng nhất.

Probe `Quota_ExistingSuccessfulLogsStillConsumeDailyLimit`: đã có **15 success
logs hôm nay**, usage trả **15**, nhưng send vẫn gọi fake provider **1 lần** và
thành công. Test cũ `Send_WhenStandardUserReachedDailyQuestionLimit_Returns429WithFriendlyMessage`
cũng nhận **200 thay vì 429** trong full suite.

**Sửa:** chuyển tiếp/backfill/lazy initialization có quy tắc, đúng UTC-day và
tokens, không tính trùng hoặc reset quota. Kiểm cả nâng cấp giữa ngày và schema
đã tồn tại nhưng chưa có counter row. Giữ daily-question và daily-token policy.

### R3 — P1: Stop vẫn bị xếp sau Start trên cùng SignalR connection

**Files:** `Hubs/ClassChatHub.cs:245–255,327–329`;
`Services/AI/AiStreamingService.cs:71–81`; `Program.cs:18`.

Gửi `AiStreamStarted` sớm giúp client biết streamId nhưng Hub vẫn await toàn bộ
provider trong invocation Start. Cấu hình thực tế
`MaximumParallelInvocationsPerClient=1`. Stop gửi trên cùng connection phải chờ
Start kết thúc nên chưa hủy được provider đang chờ.

Probe `SignalR_StopOnSameConnection_CancelsWaitingProviderBeforeProviderCompletes`
dùng **WebSocket qua TestServer + JSON SignalR protocol thật**: nhận Started,
gửi Stop, provider vẫn không nhận cancellation trong 1 giây khi fake provider
bị giữ bằng barrier. Test Cursor chỉ gọi trực tiếp `IAiStreamingService.StopStreamAsync`,
bỏ qua Hub scheduler nên pass nhưng chưa kiểm đường người dùng bấm.

**Sửa:** cancel channel/handle có ownership rõ hoặc thiết kế scheduling giới hạn
đúng AI workflow. Không tăng parallel toàn Hub một cách tùy tiện: Hub còn class
chat và scoped DbContext. Không Task.Run/fire-and-forget. Kiểm Stop trước/sau
Started, disconnect và fallback không gọi lại cùng intent đã có outcome chưa rõ.

### R4 — P2: cancellation cleanup chỉ bao quanh provider, còn hở trước/sau đó

**File:** `Services/AI/AiChatService.cs:102–176,181–223,295–302`.

Prompt được commit ở dòng103; history read, context và quota reserve nằm ngoài
catch cancellation. Probe `Cancel_AfterPromptCommitBeforeProvider_RemovesOrphan`
hủy bằng SaveChanges interceptor ngay sau commit prompt: service thoát với OCE
nhưng DB vẫn còn **1 prompt + 1 draft conversation**, provider chưa được gọi.
Hủy/lỗi khi save reply hoặc settle cũng ngoài nhánh cleanup mới.

**Sửa:** quản lý lifecycle theo giai đoạn và một failure/finalize policy xuyên
suốt sau mutation đầu tiên. Cleanup bounded, độc lập token đã hủy, giữ original
exception/lease; không xóa reply đã commit hoặc release quota khi outcome tính
phí chưa rõ. Thêm barrier/interceptor tests ở từng ranh giới.

### R5 — P2: giới hạn history đã áp dụng nhưng chưa có đường tải phần cũ

**Files:** `AiConversationService.cs:63–66,126–129`;
consumer `Controllers/AiController.cs:42–79`; AI views/JS chưa wiring pagination.

Các method cũ giờ chỉ trả 30 conversations và 50 messages, còn controller bỏ
`HasMore`. Page APIs chỉ tồn tại ở service, chưa có endpoint và UI Load older.
Người dùng không xem tiếp được history cũ bằng giao diện bình thường.

Probe `History_HttpPageProvidesAPathToOlderData` seed 36 conversations và 120
messages: GET Chat200, message/conversation cũ không xuất hiện và không có control
tải cũ. Dữ liệu chưa bị xóa trong DB, nhưng chức năng truy cập bị cắt. Không gọi
đây là BE3 đã hoàn thiện. Context Take100 cũng cần làm rõ policy/budget theo BE3.

**Sửa:** endpoint ownership404, cursor ổn định/validate, metadata và UI thật;
scroll/focus/order đúng, không duplicate/gap tại timestamp bằng nhau.

### R6 — P2: guard hash vẫn cho 500 và ChangePassword có nhánh fail-open

**File:** `Controllers/AccountController.cs:135–145,609–634,1069–1089`.

Guard chỉ kiểm prefix + length59–60. Hash 60 ký tự có prefix hợp lệ nhưng cost
không hợp lệ vẫn vào BCrypt.Verify. Probe
`Login_MalformedBcryptWithRecognizedPrefixAndLength_FailsWithout500` nhận **500,
SaltParseException**, thay vì generic login failure.

`ChangePassword` chỉ verify nếu helper trả true; helper false với hash legacy/
malformed thì bỏ qua xác minh current password. Probe authenticated fixture có
stored hash `hash` gửi current password sai vẫn đổi được hash. Phải phân biệt
social marker có policy riêng với dữ liệu hash lỗi; không tự cho phép đặt password.

**Sửa:** verify an toàn theo API thực tế của BCrypt đang pin, catch đúng lỗi parse,
không swallow lỗi không liên quan; malformed fail closed ở mọi consumer. Giữ
valid local login và OAuth contract, không auto-link identity.

### R7 — P2: validation phía server trong BE1 chưa đủ

**File:** `Controllers/AccountController.cs:75–110,551–583,590–638`.

Register mới kiểm blank/min-length nhưng chưa kiểm email format/length.
Probe `Register_InvalidEmailDoesNotPersistUser` gửi `not-an-email`: **302 và user
đã được lưu**. Đây là thiếu sót đã có trước và nằm trong scope BE1 phải hoàn tất.

`ChangePassword` không enforce ModelState/độ dài mới trước hash/save. Probe
`ChangePassword_MalformedHashDoesNotBypassCurrentPasswordAndLengthValidation`
đồng thời cho thấy password **1 ký tự** được chấp nhận trên fixture malformed.
UpdateEmail cũng mới kiểm empty. Test reset hiện kiểm replay tuần tự, chưa chứng
minh consume an toàn khi hai submit đồng thời theo checklist.

**Sửa:** validation đúng từng workflow, không dùng một VM có field bắt buộc của
workflow khác rồi chặn mọi request hợp lệ; reject trước hash/write. Thêm case
email, lengths, current/confirm, concurrent reset, antiforgery và valid paths.

### R8 — P2: test harness cũ chưa theo kịp dependency transaction mới

**File:** `TCTEnglish.Tests/AiPhase2ServiceTests.cs`, đặc biệt `CreateService` và
context builder; dependency mới `AiDailyQuotaReservationService.cs:41`.

12 test AiChatService cũ dùng EF InMemory, service mới gọi BeginTransaction nên
đều vấp TransactionIgnoredWarning/InvalidOperationException trước assertion
nghiệp vụ. Cursor chỉ thêm dependency mới vào constructor tests nhưng chưa đổi
fixture relational. Đây là 12/13 failure của full suite; failure còn lại R2 là
bug enforcement thật, không phải chỉ do fixture.

**Sửa:** chuyển test cần transaction sang relational fixture đúng semantics;
giữ assertion quota lịch sử/cancel/retry/ownership. Không suppress warning rồi
khẳng định transaction/concurrency pass; không xóa/skip test hoặc seed counter
thay logs để giấu R2. MariaDB mới là gate concurrency/provider.

## Phần đã làm được và phần còn thiếu

| Batch | Review hiện tại |
| --- | --- |
| Preflight F1/F2 | Admin CSS/matchMedia cùng991.98px; 4 test JS hành vi pass, có mutation proof. Browser breakpoint chưa chạy lại trong lượt này |
| BE0 | Chỉ baseline/interceptor; chưa có scale lab, SQL/EXPLAIN và số liệu trước–sau |
| BE1 | AccessDenied403, null-expiry, provider marker và text Việt có cải thiện; R6/R7 khiến chưa đóng |
| BE2 | Có cleanup khi provider đang chờ, started callback và schema quota; R1–R4 chặn nghiệm thu |
| BE3 | Có SQL Take/cursor ở service; R5 + UI/API và budget context còn thiếu |
| BE4 | Speaking/Writing SQL pagination/aggregates **chưa làm** |
| BE5 | Có SMTP timeout30s; fake transport/caller cancel/result semantics/unlock idempotency chưa làm |
| BE6 | Profile badge-only query/launcher query reuse **chưa làm** |
| BE7 | Asset pin/cascade/trace/sanitized diagnostics **chưa làm** |
| BE8 | Full suite hiện đỏ; MariaDB schema/concurrency, browser và các acceptance còn thiếu |

Ledger Cursor có ghi “mid-handoff”, “PARTIAL/PENDING” và không claim full Plan2.
Không nên hiểu bàn giao này là đã hoàn tất toàn bộ yêu cầu tối ưu.

Kế hoạch sửa và hoàn tất: [repair plan](../superpowers/plans/2026-11-02-cursor-plan2-review-repair.md).
Prompt giao Cursor Auto: [execution prompt](../../.ai/prompts/cursor-plan2-review-repair.md).

Sau khi sửa, giữ bằng chứng cũ, thêm kết quả mới trong ledger; chỉ reviewer/owner
mới đóng các finding bằng kiểm chứng tương ứng. Không sửa bug log thành “đã fix”
cho R1–R8 khi chưa implement/verify. Production gate vẫn false.
