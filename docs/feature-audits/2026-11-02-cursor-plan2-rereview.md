# Review lại Cursor Plan 2 — vòng 2, 02/11/2026

**Kết luận: chưa nghiệm thu Plan 2.** Bản sửa vượt qua 9 probe của vòng trước
và full suite hiện hữu đã xanh. Review mở rộng tìm thấy 6 finding còn lại/new
regression. BE0/BE4–BE8 vẫn thiếu phần thực thi như ledger Cursor tự ghi.

Nhánh `feature/chau-ui-performance-audit`, HEAD210bf2d. Một agent review inline,
không sửa runtime, commit/push/CI/PR/merge/deploy. Giữ UI Plan1 và dữ liệu dev.
Gate GitHub đọc lại: `PLESK_DEPLOYMENT_ENABLED=false`. Website model giữ
`gpt-6-luna`/low; tất cả AI probes dùng fake provider, không gọi API trả phí.

## Bằng chứng mới của reviewer

| Check | Kết quả | Phạm vi |
| --- | --- | --- |
| Fresh full application suite `CursorRepairReview`, Optimize=true | **838 pass / 0 fail / 10 skipped / 848**,3m | Không dùng DLL cũ; acceptance flags MariaDB/live tắt trong lượt này |
| 9 `CursorReviewProbes` cũ | **9/9 pass**, nằm trong suite trên | Đóng đúng 9 trigger cũ, chưa chứng minh tất cả invariants của RP0–RP6 |
| JS submit-guard | **4/4 pass** | Production JS trong fake DOM/fetch |
| Hai test MariaDB của Cursor, reviewer chạy lại | **2/2 pass** | Overlapping scopes reserve/settle tuần tự và historical15logs |
| MariaDB reserve đồng thời bổ sung | **1 fail / 1** | Hai transaction cùng SELECT, còn2slots; deadlock chưa recover |
| Bốn application probe bổ sung | **4 fail / 4** | Timestamp, finalize-cancel, retry duplicate provider, UpdateEmail validation |
| Chrome DOM với production `loadOlderMessages` + fake fetch | Tái hiện đảo thứ tự | Kiểm real DOM, không phải browser acceptance cả website |
| EF pending-model check | Sạch | Model/snapshot khớp, không thay thế upgrade/preservation proof |
| `git diff --check` | Pass | Có LF/CRLF warnings, không có lỗi whitespace |

Artifacts ignored tại `.tmp/cursor-repair-review/`:

- `cursor-repair-review.trx`, `full-run.log` — full suite.
- `repair-review-probes.trx`, `probes.log` — 4 probe mới.
- `mariadb-quota-review.trx`, `mariadb-probes.log` — 2 pass +1 fail trên MariaDB.
- `RepairReviewTests.cs`, `RepairReview.targets` — regression chẩn đoán thêm;
  import chỉ khi command test chỉ định, không sửa `.csproj`.
- `browser-paging-order.json` — actual/expected order của Chrome DOM probe.

Không cộng các probe đỏ vào số full suite xanh rồi gọi toàn bộ acceptance pass.
Số mới khác ledger Cursor844/0/5/849: đây là lệnh/config/flags và kết quả fresh
của reviewer; chỉ báo actual counters tương ứng, không suy luận các lượt là giống nhau.

MariaDB dùng đúng disposable có sẵn của Cursor:
`tct_acceptance_p2rp1102a`, container `tctenglish-mariadb-acceptance-p2rp1102a`,
loopback3328,10.5.23, runId`p2rp1102a`. Factory xác nhận sentinel trước xóa/seed
fixture. Credentials chỉ qua child environment, không log/argv/Git. Không đụng
`tctenglish_dev`, production, sentinel hay migration history.

## Findings hiện tại

### C1 — P1: reserve thực sự đồng thời trên MariaDB gây deadlock không recover

**File:** `Services/AI/AiDailyQuotaReservationService.cs:40–46,81–86`.
Runtime `Program.cs` đăng ký UseMySql không có transient retry policy.

Probe `RepairMariaDbConcurrencyReview.SimultaneousReservations_WithTwoAvailableSlots_BothSucceed`
seed13settled, còn2slot. Hai DbContext có barrier sau SELECT quota để thật sự
cùng đọc trong hai Serializable transactions rồi cùng reserve.

Kết quả: **chỉ1request thành công, PendingQuestions=1**, request còn lại ném
InvalidOperationException, root MySqlException:
`Deadlock found when trying to get lock; try restarting transaction`.

`CreateExecutionStrategy()` hiện không tự biến cấu hình mặc định thành retry.
Hai test MariaDB Cursor mới thêm gọi Areserve→Breserve→Asettle→Bsettle tuần tự,
nên pass nhưng chưa kiểm thời điểm lock cạnh tranh.

**Sửa:** locking/atomic mutation phù hợp MariaDB và bounded transaction retry
có fresh state, idempotent identity qua retry. Kiểm first-row creation, lastslot,
hai available slots và settle/release tranh chấp. Không giữ lock qua provider,
không chỉ bật global retries rồi bỏ commit ambiguity/duplicate.

### C2 — P1: hủy lúc finalize nhả quota dù reply đã commit

**File:** `Services/AI/AiChatService.cs:262–271,294–318`.

Service có `replyCommitted=true` để không xóa reply nhưng vẫn Release reservation
vô điều kiện trong catch OCE. Probe
`CancelAtFinalize_AfterCommittedReply_DoesNotReleaseChargedReservation` chặn tại
Settle sau khi provider đã trả và SaveChanges đã xong, rồi hủy request.

DB thật của fixture: **1assistant +1successlog**, ledger **Released**, aggregate
**pending0/settled0**. Lượt đã tạo thành công biến mất khỏi quota authority, trong
khi usage UI vẫn tính log. Outcome provider không thể được coi như chưa gửi.

**Sửa:** terminal state theo giai đoạn, finalize bounded độc lập caller-abort
khi đã có kết quả, hoặc durable pending/unknown có reconciliation. Không release
reply đãcommit/đãbiết token; không xóa reply để che inconsistency. Quy tắc phải
bao phủ cả save/finalize exception/crash, không chỉ OCE trong provider.

### C3 — P1: retry sau lỗi finalize có thể gọi provider hai lần cùng intent

**Files:** `AiChatService.cs:265–292`;
`wwwroot/js/ai-chat.js:571–594`; request contract/ledger identity.

Probe `RetryAfterCommittedReplyFailure_DoesNotCallProviderTwiceForSameIntent`
cho provider thành công, save reply xong rồi inject finalize exception. HTTP
trả500. Probe lặp lại cùng POST/payload cho existing conversation theo đúng
retry condition hiện có của JS (`>=500`, existingconversation cho phép2attempts).

Kết quả: **500 rồi500, providerCalls=2, assistantRows=2**. RequestId quota là
Guid ngẫu nhiên mới mỗi reserve, chưa phải client intent identity xuyên HTTP/
SignalR. Nó làm settle idempotent cho một reservation nhưng chưa dedup send.

Đây là integration probe thực hiện hai HTTP requests theo policy JS; không claim
đã click UI và quan sát paid API. Source `sendWithRetry` xác nhận policy tự retry.

**Sửa:** khi outcome không rõ, không tự gửi lại gây phí; request identity/payload
binding và known-result/recovery nếu bổ sung. Giữ routes/JSON tương thích. Không
hứa remote billing exactly-once; exception saucommit không được biến thành lượt
provider mới một cách âm thầm. Test reconnect/fallback cùng intent như RP3 gốc.

### C4 — P2: Load older đảo thứ tự trong từng page tin nhắn

**File:** `wwwroot/js/ai-chat.js:1326–1362`.

API trả Items chronological. Vòng lặp luôn insert từng row trước
`insertBefore.nextSibling`, tức cùng một vị trí ngay sau control. Vì thế page
`m0,m1,m2,m3` được đảo thành `m3,m2,m1,m0`.

Chrome real-DOM probe chạy đúng production function, chỉ fake fetch/hydration:
ban đầu `[m4,m5]`, payload `[m0,m1,m2,m3]`; DOM thực tế
**`[m3,m2,m1,m0,m4,m5]`**, expected`[m0,m1,m2,m3,m4,m5]`.
Test cũ chỉ kiểm button tồn tại, chưa chạy đường load này.

**Sửa:** chèn một fragment/page giữ đúng thứ tự hoặc dùng insertion reference
ổn định, kiểm nhiều page/timestamp ties/scroll+focus. Reuse renderer phù hợp
để phần tải cũ có cùng avatar/actions/Markdown conventions; kiểm delete action
cho conversations tải thêm và error state khi fetch thất bại.

### C5 — P2: ChangeTracker.Clear trong quota làm mất cập nhật hội thoại của caller

**Files:** `AiDailyQuotaReservationService.cs:43–44,117`;
`AiChatService.cs:80–88,261–262`.

Quota và chat dùng cùng scoped DbContext. Clear detach toàn bộ entity của caller,
gồm `conversation`. Sau provider, gán UpdatedAtUtc lên object detached không
được SaveChanges lưu.

Probe `Send_SuccessAdvancesConversationTimestamp`: existing conversation có
UpdatedAt ba ngày trước. Send thành công, lưu đủ2messages, nhưng **UpdatedAt
trước/sau y hệt nhau**. Reload history giữ vị trí cũ; hội thoại có thể nằm ngoài
page30 dù vừa được sử dụng. R1 stale counter được né bằng thao tác có side effect
khác, không nên dùng Clear toàn context ở service con.

**Sửa:** isolate quota unit of work hoặc refresh đúng quota entries/atomic SQL;
giữ tracking/các pending changes của caller. Test persisted recency và thứ tự
history sau reload, không chỉ DOM touchConversationHistory ở client.

### C6 — P2: BE1 validation vẫn hở ở UpdateEmail; reset concurrent còn thiếu proof

**File:** `Controllers/AccountController.cs:550–580`.

Register đã có email validator, nhưng UpdateEmail vẫn chỉ check empty. Probe
`UpdateEmail_InvalidEmailMustNotPersist` gửi Email`not-an-email` với auth/CSRF hợp
lệ: **302 và giá trị không hợp lệ đã được lưu** vào user. Đây là thiếu sót cũ nằm
trong BE1/R7 phải sửa, chưa được đóng bằng các tests Register/ChangePassword.

Ngoài ra ResetPassword vẫn load/check/token-null/save, tests mới chỉ replay tuần
tự; chưa có proof hai submit đồng thời chỉ một lần consume. Phần này ghi thiếu
acceptance, không claim reviewer đã chạy race reset trong lượt này.

**Sửa:** validation đúng từng workflow (format/length/normalization/uniqueness),
không check mù sharedVM buộc nhập password khi đổi email. Test valid/invalid và
reset concurrent theo repair plan trước khi ghi BE1/RP5done.

## Đối chiếu R1–R8 cũ và phần đã cải thiện

| Phần | Đánh giá vòng2 |
| --- | --- |
| R1 stale counters/replay | Hai trigger cũ pass cả overlap MariaDB; true contention C1 và caller tracking C5 chưa đóng |
| R2 historical quota | Probe15logs và MariaDB15logs pass; cần giữ bằng chứng transition/preservation theo schema |
| R3 Stop khi provider chờ | Probe WebSocket thật nay pass nhờ parallel2; intent/fallback safety C3 vẫn thiếu |
| R4 cancel sauprompt | Probe cũ pass; postcommit finalize C2 chưa đúng |
| R5 history | Có endpoint/control thật; load order C4 chưa đúng, browser acceptance chưa xong |
| R6 malformedbcrypt | Probe cũ không còn500; hash-invalid ChangePassword đã failclosed trong case đã kiểm |
| R7 validation | Register email/minpassword đã sửa; UpdateEmail C6 và reset concurrent chưa đóng |
| R8 InMemorytransactions | Test fixture đã dùng SQLite; full suite cũ xanh, không còn13fail vòng1 |

**Phần Plan2 chưa thực thi:** BE0 scale lab30samples; BE4 Speaking/Writing SQL
paging/aggregates; BE5 fake SMTP/caller cancellation/unlock semantics; BE6 Profile
badge query/launcher reuse; BE7 assets/pinning/trace/diagnostics; BE8 browser role/
mobile/ASR/cleanPC/external gates. Ledger Cursor ghi rõ pending/partial nên không
đánh đồng với đã xong. Không báo whole RP0–RP6done chỉ vì9probe cũ pass.

Migrations mới có migration+Designer+snapshot; EFmodel sạch. Reviewer chạy lại
quota tests trên disposable đã được Cursor migrate; **không tự chạy lại toàn
bài upgrade từ schema cũ/preservation** trong lượt này. Giữ đúng giới hạn bằng chứng.

Kế hoạch: [repair vòng2](../superpowers/plans/2026-11-02-cursor-plan2-rereview-repair.md).
Prompt: [Cursor Auto](../../.ai/prompts/cursor-plan2-rereview-repair.md).
