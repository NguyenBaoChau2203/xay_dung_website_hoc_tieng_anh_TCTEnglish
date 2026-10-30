# Ledger thực thi — Nâng cấp độ tin cậy chức năng học (Bảo Châu)

> Cập nhật bởi **một agent** Cursor theo plan `docs/superpowers/plans/2026-10-29-bao-chau-learning-reliability-plan.md` và prompt `.ai/prompts/cursor-grok-bao-chau-learning-upgrade.md`.

| Trường | Giá trị |
| --- | --- |
| Lô hiện tại | **F0–F9 remediation** — code/test một phần; **không** production-ready |
| Thời điểm L0 | 2026-10-30 (UTC+7) |
| Thời điểm L1 | 2026-10-30 (UTC+7) |
| Thời điểm L2+L3 | 2026-10-30 (UTC+7) |
| Thời điểm L4–L7 | 2026-10-30 (UTC+7) |
| Quyền L1 được cấp | JS/view người học + Node regression tests; **không** Program/config/package/schema/engine |
| Quyền L2+L3 được cấp | Writing options + clock DI trong `Program.cs`; migration additive **chỉ tạo/review**, **không** apply DB thật |
| Quyền L4–L7 được cấp | “hoàn thành toàn bộ lô còn lại”: Speaking assess DI/options/migration, import job, credit/outbox, Writing engines **default off**; **không** apply DB thật, **không** tải model/whisper lớn, **không** mở cổng public |
| Agent policy | Một agent; không worker/reviewer/CLI AI khác |
| Source sau L7 | Core L4–L6 + adapter L7; hunk UI dirty Antigravity giữ nguyên |

---

## 1. Baseline Git và thay đổi UI đang mở

| Mục | Bằng chứng |
| --- | --- |
| HEAD | `3d27a68c8468207eca5a11653c72e735117fc637` — khớp khảo sát |
| Nhánh | `feature/bao-chau-ui-audit` |
| Commit tip | `3d27a68 chore(tooling): modernize shared agent skills and team setup (#119)` |
| Diff đã commit vs working tree | **19 file source/CSS sửa**, `+421 / −190` (không stash/reset) |

### 1.1 File modified (UI/Antigravity — không coi là việc của Cursor)

| File | Ghi chú xung đột với lô học |
| --- | --- |
| `Views/Speaking/Index.cshtml` | UI; trong phạm vi Speaking nhưng chỉ polish — L1 tránh redesign |
| `Views/Speaking/Practice.cshtml` | **aria-label / aria-hidden** trên `#btn-prev/next/replay`, dictation controls, `#dic-input` — giữ hunk; L1 chỉ thêm nhãn trạng thái nếu thật sự cần |
| `Views/Study/Speaking.cshtml` | Clamp padding/font-size CSS inline — ngoài JS mic; L1 không đụng |
| `Views/Study/WritingExercises.cshtml` | UI polish |
| `Views/Study/WritingPractice.cshtml` | +`aria-label` textarea — L2 sau này tôn trọng |
| `wwwroot/css/writing.css`, `wwwroot/css/goals.css` | CSS; không redesign trong task chức năng |
| `Views/Account/*` (6 file), `wwwroot/css/account.css` | **Ngoài phạm vi** (Auth) — không đụng |
| `Views/Home/Contact.cshtml` | **Ngoài phạm vi** |
| `Views/Ai/_ChatShell.cshtml` | **Ngoài phạm vi** (AI Chat) |
| `Areas/Admin/Views/...` (3 file) | **Ngoài phạm vi** (Admin) |

### 1.2 File JS nghiệp vụ L1 — sạch trong working tree

| File | Trạng thái |
| --- | --- |
| `wwwroot/js/speaking.js` | **Không dirty** — an toàn sửa ở L1 |
| `wwwroot/js/notifications.js` | **Không dirty** — an toàn sửa ở L1 |
| `Views/Notification/Index.cshtml` | **Không dirty** — an toàn sửa inline script ở L1 |

### 1.3 Untracked (tài liệu / prompt)

- `.ai/prompts/antigravity-bao-chau-ui-analysis.md`
- `.ai/prompts/cursor-grok-bao-chau-learning-upgrade.md`
- `.cursor/settings.json`
- `docs/feature-audits/2026-10-29-bao-chau-learning-reliability-audit.md`
- `docs/feature-audits/bao-chau-learning-execution-ledger.md` *(file này)*
- `docs/superpowers/plans/2026-10-29-bao-chau-learning-reliability-plan.md`
- `docs/superpowers/plans/2026-10-29-bao-chau-ui-audit-plan.md`
- `docs/ui-audits/2026-10-29-bao-chau-ui-analysis.md`

**Quyết định L0:** giữ nguyên mọi hunk UI; không format/checkout. Nếu Antigravity còn sửa `Practice.cshtml` / Writing views, L1 ưu tiên `speaking.js` + `notifications.js` + `Notification/Index` và chỉ chạm view khi cần nhãn trạng thái, tránh ghi đè aria.

---

## 2. Ngữ cảnh đã đọc / công cụ

Đã đọc: `AGENTS.md`, `.ai/README.md`, audit, plan, prompt, `known-issues.md` (BUG-007/008 + TD), đầu `bug-fix-log.md`, `coding-conventions.md`, skills `tct-superpowers`, `tct-engineering`, `tct-speaking-feature`.

**CodeGraph:** MCP namespace `codegraph` **không** có trong Cursor session này. Dùng CLI pinned qua `powershell -NoProfile -File scripts/codegraph.ps1` (không `pwsh`). Index hiện có: 497 files / 8 083 nodes / 17 689 edges; DB ~30.5 MB; **Pending Added 22 / Modified 16**; engine index ghi `v1.5.0` vs tooling mới hơn — **không** chạy `index`/`sync` theo chính sách chủ nhóm. Explore đã chạy (đọc source on-disk): `SpeakingController SaveSpeakingProgress`, `WritingService EvaluateWritingSentenceAsync`, `NotificationService`, `StreakService UpdateStreakAsync`. Phần JS/view không đủ trong graph → đọc bounded file.

**Known issue liên quan vận hành:** BUG-008 — không `dotnet run` trên DB thật (seeder ghi DB mỗi startup).

---

## 3. Stack và GoalArea (xác nhận source)

| Mục | Source |
| --- | --- |
| TFM | `TCTEnglish.csproj` → `net9.0` |
| EF | `Microsoft.EntityFrameworkCore` **9.0.4** |
| DB provider | `Pomelo.EntityFrameworkCore.MySql` **9.0.0**; `Program.cs:28` `UseMySql(..., ServerVersion.AutoDetect(...))` |
| SDK máy | .NET SDK **10.0.300**; runtime ASP.NET Core **9.0.16** có sẵn — **không** nâng project lên net10 |
| GoalArea | Enum đủ 5: Vocabulary, Speaking, Writing, Reading, Listening (`Models/UserGoal.cs`). `GoalsService.GoalAreasWithRealCompletionSignals` gồm **cả năm**. Không tắt Reading/Listening theo backlog cũ. |

---

## 4. Hợp đồng API / ViewModel hiện tại

### 4.1 Speaking progress

- **Route:** `POST /api/speaking/{sentenceId}/progress`
- **Auth/CSRF:** `[Authorize]`, `[ValidateAntiForgeryToken]`; client gửi header `RequestVerificationToken` từ `input[name="__RequestVerificationToken"]`
- **Body (`SpeakingProgressDto`):** `totalScore`, `accuracyScore`, `fluencyScore`, `completenessScore` (double 0–100)
- **Ownership:** sentence → video; private owner ≠ current → `NotFound`; owner Standard → `NotFound`
- **Response 200:** `{ success, sentenceId, videoId, totalScore, accuracyScore, fluencyScore, completenessScore, passedSentenceCount, requiredSentenceCount, isVideoCompleted, firstTimeVideoCompletion, xpEarned, streak }`
- **Ngưỡng:** pass câu ≥70; hoàn thành video ≥ ceil(70% số câu); first completion → `RecordLearningActivityAsync`
- **Rủi ro S02:** server tin điểm client; L1 chỉ ngừng gọi từ UI/Web Speech — **không** khóa endpoint

### 4.2 Speaking client (`speaking.js`) — xác nhận S01

- Thiếu `SpeechRecognition` → toast demo → `simulateFeedback()` → `Math.random` 78–97 → **POST lưu thật**
- Nhánh nhận dạng: `applyRecordingFeedback` cộng nhiễu `Math.random` vào accuracy/fluency/completeness rồi POST
- Selector chính giữ nguyên: `#btn-record`, `#record-label`, score board `#score-total` / `#score-accuracy` / `#score-fluency` / `#score-complete`, progress `#prog-*`, sentence data `SENTENCES[i].id`

### 4.3 Writing evaluate

- **Route:** `POST /Home/Writing/Practice/Evaluate` (StudyController + legacy Home)
- **Request:** `{ exerciseId, sentenceId, userAnswer }`
- **Response:** `{ success: true, data: WritingSentenceEvaluationViewModel }`
- **VM fields hiện tại:** `exerciseId`, `sentenceId`, `sentenceNumber`, `passed`, `canAutoAdvance`, `usedAi`, `evaluationSource`, `summaryTitle/Text`, `reviewText`, meaning/grammar/naturalness/wordChoice feedback, `suggestedRewrite`, attempt/progress counts, `exerciseStatusKey/Label`
- **L2 dự kiến (chưa áp):** thêm `assessmentStatus`, `assessmentSource`, `reviewRequired`; giữ field cũ; `passed=true` / auto-advance chỉ khi Accepted; mode Local mặc định không chờ cloud

### 4.4 Notifications

- **Page:** `GET /Notification?page=&filter=` — load tới 500 rồi filter/page trong RAM (N04)
- **API:**
  - `GET /api/notifications/unread-count` → `{ count }`
  - `GET /api/notifications?page&pageSize` (clamp 1–50) → `{ success, data: { items, unreadCount, currentPage, hasMore } }`
  - `POST /api/notifications/{id}/mark-read` → `{ success: true }` (+ CSRF)
  - `POST /api/notifications/mark-all-read` → `{ success: true }` (+ CSRF)
- **`NotificationListViewModel`:** `Items`, `UnreadCount`, `CurrentPage`, `TotalPages`, `HasMore`
- **Client N05:** `notifications.js` `handleItemClick` bỏ unread trước, không kiểm `res.ok`; luôn `n.isRead = true`. `Index.cshtml` tương tự với mark đơn; mark-all page: HTTP lỗi không restore `disabled` (chỉ catch mạng)

### 4.5 Premium / ownership (Speaking cá nhân)

Đã thấy trong `SaveSpeakingProgress`: video `OwnerUserId` + role Standard bị chặn bằng `NotFound`. Import Premium giữ nguyên cho L5; L0 không đổi.

---

## 5. Môi trường và test host

| Mục | Giá trị quan sát |
| --- | --- |
| OS | Windows 11 Home Single Language, build 10.0.26300, RID `win-x64` |
| CPU | Intel i5-11400H, 6C/12T @ 2.70 GHz |
| RAM | ~16 GB tổng; free lúc đo ~1 GB (máy đang tải) |
| GPU | NVIDIA GeForce RTX 3050 Ti Laptop (~4 GB VRAM báo cáo) + Intel UHD |
| Node | v24.14.0 (đủ cho `node --test` L1 sau này) |
| Test host | `TestWebApplicationFactory`: SQLite `:memory:`, stub/deterministic AI (`InternalKnowledgeProvider`), bỏ `AutoUnlockWorker`; **không** chứng minh MySQL unique/race hay Gemini thật |
| Hạ tầng ASR | Máy dev có GPU/RAM vừa phải — **đủ thử spike L4 sau**, **chưa** chứng minh production-ready; L0/L1/L2 không bị chặn vì thiếu ASR |
| Browser/mic/YouTube | **Chưa** chạy trong L0 |

---

## 6. Kiểm thử nền L0 (mục 11 plan)

Thư mục log: `.tmp/learning-reliability-upgrade/`

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln --no-restore --verbosity minimal` | **Succeeded**, 0 Warning(s), 0 Error(s), ~6.4 s → `build.log` |
| `dotnet test ... --filter "FullyQualifiedName~Speaking\|...\|Notification" ...` | **Passed 111 / Failed 0 / Skipped 0**, ~37 s → `test.log` + `test-results/learning-upgrade.trx` |
| `git diff --check` | Exit 0 (chỉ cảnh báo CRLF trên vài view) |

Số 111 khớp baseline khảo sát; đây là lần chạy Cursor L0, không phải bằng chứng đã sửa S01–N06. Không thấy regression sẵn trong filter này cần “vá ngoài phạm vi”.

---

## 7. Xác nhận phát hiện ưu tiên (source còn đúng)

| ID | Còn đúng sau L0? | Ghi chú ngắn |
| --- | --- | --- |
| S01 | Có | `speaking.js` ~402–404, 605–607, 641–653 |
| S02 | Có | `SpeakingController` ~118–257 nhận điểm client |
| N05 | Có | `notifications.js` ~203–221; `Notification/Index` ~201–260 |
| W01/W02 | Có (CodeGraph + VM) | Evaluate vẫn đi AI path / fallback yếu — chi tiết L2 |
| G01 | Có | Completion rồi mới reward trong Speaking path |
| N01–N04, N06 | Có theo audit; chưa re-benchmark timezone trong L0 | Giữ cho L3 |
| S03–S04 | Hoãn L4/L5 | Không cài engine ở L0 |

---

## 8. Ma trận quyền / approval

| Lô | Trạng thái approval |
| --- | --- |
| L0 | **Được yêu cầu và hoàn tất** |
| L1 | **Được duyệt và hoàn tất** |
| L2 | **Được duyệt** (cùng L3) và hoàn tất |
| L3 | **Được duyệt** (cùng L2) và hoàn tất — migration **chưa** apply DB thật |
| L4–L7 | **Được duyệt** (“hoàn thành toàn bộ lô còn lại”) và hoàn tất code; migrations **chưa** apply; ASR/LT/Ollama **chưa** chứng minh production |

---

## 9. Đề xuất lô triển khai đầu tiên

**Bắt đầu `L1`** (sau khi chủ nhóm nói rõ “thực hiện L1” hoặc “L0 và L1”):

1. Tạo Node regressions đỏ: `scripts/tests/test_speaking_behavior.cjs`, `test_notification_behavior.cjs` (fixture DOM/recognition/fetch; chạy code client thật).
2. Sửa `speaking.js`: bỏ `simulateFeedback` lưu thật và `Math.random` trong điểm; Web Speech → **preview-only** (không POST progress/XP tới L4).
3. Sửa `notifications.js` + inline `Views/Notification/Index.cshtml`: chỉ xác nhận đã đọc khi HTTP thành công; rollback/retry nút.
4. Tránh hunk aria trên `Practice.cshtml` nếu không bắt buộc; không đụng Auth/Contact/AI Chat/Admin/CSS redesign.
5. Residual: **S02 vẫn mở** cho tới L4.

Giá trị cao tiếp theo sau L1: **L2** (Writing Local) rồi **L3** (ngày/dedup/paging). L4/L5 cần duyệt máy/model riêng.

---

## 10. Kết luận L0

**Đã hoàn tất preflight; chưa sửa ứng dụng.** (L0 đóng; L1 đã được duyệt và thực hiện bên dưới.)

---

## 11. L1 — Loại kết quả bịa và phản hồi đọc thông báo

### 11.1 Hành vi trước → sau

| Hạng mục | Trước | Sau |
| --- | --- | --- |
| Thiếu SpeechRecognition | Demo + `Math.random` 78–97 POST lưu thật | Báo không khả dụng; **không** POST; fluency/completeness “—” |
| Web Speech có transcript | Edit-distance + nhiễu random → POST progress/XP | **Preview-only**: hiện mức khớp lời nói; không POST; không `isPracticed`; fluency/completeness “Chưa đánh giá” (`—`) |
| Đổi câu khi đang ghi | Callback cũ có thể gắn nhầm | Snapshot epoch/sentenceId; abort khi đổi câu; callback muộn bị bỏ |
| Mark-read / mark-all | Optimistic / bỏ unread trước; lỗi vẫn coi đã đọc | Chỉ xác nhận khi HTTP+payload OK; rollback unread; nút mark-all retry được sau lỗi HTTP/mạng |
| Điểm lịch sử DB | — | **Không** xóa/tính lại |

### 11.2 File đã đổi (L1)

- `TCTEnglish/wwwroot/js/speaking.js` — bỏ demo/rand; preview-only; snapshot; dispose; `TctSpeakingPractice` hooks (test)
- `TCTEnglish/Views/Speaking/Practice.cshtml` — thêm `#spk-assessment-status` (giữ hunk aria Antigravity)
- `TCTEnglish/wwwroot/js/notifications.js` — mark-read/mark-all an toàn; test hooks khi `TCT_NOTIFICATIONS_TEST_MODE`
- `TCTEnglish/Views/Notification/Index.cshtml` — cùng hợp đồng mark-read/mark-all
- `scripts/tests/dom_fixture.cjs` — fixture DOM tối thiểu
- `scripts/tests/test_speaking_behavior.cjs` — regression Speaking
- `scripts/tests/test_notification_behavior.cjs` — regression Notifications
- `docs/feature-audits/bao-chau-learning-execution-ledger.md` — ledger này

### 11.3 Kiểm chứng L1

```text
node --test --test-force-exit --test-concurrency=1 scripts/tests/test_speaking_behavior.cjs scripts/tests/test_notification_behavior.cjs
→ 11 passed / 0 failed

dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln --no-restore
→ 0 Error(s) (cảnh báo CS sẵn có ngoài phạm vi L1)

dotnet test ... --filter "FullyQualifiedName~Speaking|Writing|Goals|Notification"
→ 111 passed / 0 failed (lần chạy lại ổn định; một lần trước có flake SQLite ObjectDisposedException song song, không liên quan diff L1)
```

Browser mic/HTTPS thật: **chưa** chạy trong L1 (không có phiên học hợp lệ trên DB thật theo BUG-008).

### 11.4 Residual risk / đánh đổi

- **S02 còn mở**: `POST /api/speaking/{id}/progress` vẫn nhận điểm client tới L4.
- Speaking video tạm **không lưu tiến độ/XP** từ nhánh Web Speech đến khi L4 có assessment server — người học vẫn nghe/thu/xem trước.
- `Views/Study/Speaking.cshtml` chỉ so khớp local, không POST progress API — không đổi trong L1.
- Không redesign CSS; không đụng hunk UI Auth/Contact/AI/Admin đang dirty.

### 11.5 Lô tiếp theo đề xuất

L2+L3 đã được duyệt và thực hiện (mục 12–13).

**Kết luận L1:** Đã bỏ demo/điểm bịa phía client Speaking và sửa success giả khi đọc thông báo. Chưa khóa API legacy (S02). Chưa commit/push.

---

## 12. L2 — Writing Local không chờ cloud

### 12.1 Hành vi trước → sau

| Hạng mục | Trước | Sau |
| --- | --- | --- |
| Chấm câu mặc định | Thường chờ AI / fallback rule-based sau cloud | **Local** mặc định: `WritingAssessmentPolicy` quyết định Accepted / NeedsRevision / NeedsReview |
| Auto-pass | Có thể coverage/keyword yếu | Chỉ **khớp chuẩn hóa** với đáp án tham chiếu → Accepted; khác cách diễn đạt → NeedsReview (không auto-pass) |
| Cloud | Có thể quyết định pass | `CloudEnhancementEnabled=false` mặc định; bật thì chỉ bổ sung feedback gắn nhãn, **không** đổi `passed` ở Local |
| VM JSON | `passed`, `evaluationSource`, … | Thêm `assessmentStatus`, `assessmentSource`, `reviewRequired`; giữ field cũ (`evaluationSource` = source Local, vd. `local-exact`) |
| Scope guard | Hard-fail trước AI | Giữ nguyên (vẫn `rule-based` / không gọi AI) |

### 12.2 File đã đổi / tạo (L2)

- `TCTEnglish/Services/Writing/WritingEvaluationOptions.cs`, `WritingAssessmentPolicy.cs`
- `TCTEnglish/Services/WritingService.cs`, `IWritingService.cs`
- `TCTEnglish/ViewModels/WritingIndexViewModel.cs` — assessment fields
- `TCTEnglish/Controllers/StudyController.cs` — surface assessment (nếu có)
- `TCTEnglish/Program.cs` — `AddOptions<WritingEvaluationOptions>().BindConfiguration("Learning:Writing")`
- `TCTEnglish/wwwroot/js/writing.js` — đọc assessment + copy NeedsReview
- `docs/operations/writing-local-evaluation.md`
- Tests: `WritingLocalAssessmentTests.cs`, `WritingAvailabilityIntegrationTests.cs`; cập nhật AI/Persistence/opt-in Cloud + `Sprint1SmokeTests` (`local-exact`)

### 12.3 Kiểm chứng L2 (gộp với L3 — mục 13.3)

Không apply config secrets; không đổi `AI:*` toàn cục.

### 12.4 Residual

- Cloud enhancement opt-in chưa chứng minh Gemini production.
- Câu paraphrase hợp lệ vẫn NeedsReview tới khi có đáp án biến thể được duyệt (đánh đổi sản phẩm L2).
- Không đụng Writing CSS dirty / Auth.

---

## 13. L3 — Goals/Notifications đúng ngày, dedup, paging, worker

### 13.1 Hành vi trước → sau

| Hạng mục | Trước | Sau |
| --- | --- | --- |
| Ngày nghiệp vụ | Lệch / helper rải rác | `LearningBusinessClock` (UTC+7) + DI singleton; Streak dùng clock |
| Dedup thông báo | Có thể trùng | `Notification.EventKey` + unique `(UserId, EventKey)`; `TryCreateWithEventKeyAsync` bỏ qua trùng / detach khi conflict |
| Paging | Load ~500 rồi lọc RAM | Query DB `Skip/Take`, clamp pageSize 1–50; unread count riêng |
| Worker | Không checkpoint rõ | `NotificationJobRun` (JobName+BusinessDate unique), lease/attempt; `TryRunDueJobsAsync` idempotent |
| Client poll | — | `notifications.js`: visibility + backoff khi tab ẩn / lỗi |
| Streak badge | — | `PreviousLongestStreak` hỗ trợ so sánh longest |

### 13.2 File đã đổi / tạo (L3)

- `TCTEnglish/Services/Notifications/LearningBusinessClock.cs` (+ folder)
- `TCTEnglish/Models/NotificationJobRun.cs`; `Notification.EventKey`
- `TCTEnglish/Models/DbflashcardContext.cs` — DbSet + indexes
- `TCTEnglish/Migrations/20261029175518_LearningNotificationReliability.cs` (+ Designer) — **additive only**; Down chỉ drop cột/bảng/index mới
- Snapshot `DbflashcardContextModelSnapshot.cs`
- `NotificationService` / `INotificationService` / `NotificationController` / ViewModels
- `NotificationWorker.cs`; `StreakService` / `StreakUpdateResult`
- `Program.cs` — đăng ký `LearningBusinessClock`
- `wwwroot/js/notifications.js` — visibility/backoff (giữ hợp đồng mark-read L1)
- Tests: `NotificationReliabilityIntegrationTests.cs`, `NotificationWorkerRecoveryTests.cs`

**Chưa:** `dotnet ef database update` / apply MySQL thật.

### 13.3 Kiểm chứng L2+L3

```text
dotnet test ... --filter "Speaking|Writing|Notification|Goals|Streak|Sprint1Smoke"
→ 152 passed / 0 failed (~33 s)

node --test scripts/tests/test_speaking_behavior.cjs scripts/tests/test_notification_behavior.cjs
→ 11 passed / 0 failed

Filter hẹp Writing*|NotificationReliability|NotificationWorker|Speaking|GoalsPhase
→ 91 passed / 0 failed (trước khi mở rộng Sprint1)
```

SQLite test host **không** chứng minh unique/race MySQL production. Migration chỉ review trong repo.

### 13.4 Residual / đánh đổi

- Migration **chưa apply** — EventKey/JobRuns chưa có trên DB thật tới khi chủ nhóm duyệt apply.
- Unique MySQL với nhiều `EventKey` null: chấp nhận lịch sử/broadcast; không dedup Admin bằng key rỗng.
- Goals vẫn có chỗ dùng `BusinessDateHelper` cũ — clock đã gắn Streak/Notification path chính; quét hết Goals là residual nhẹ.
- S02 Speaking API vẫn mở (L4).
- Không commit/push trong phiên này.

### 13.5 Lô tiếp theo đề xuất

L4–L7 đã được duyệt và thực hiện (mục 14–17).

**Kết luận L2+L3:** Writing Local mặc định không chờ cloud; Notifications/Goals có clock UTC+7, EventKey dedup, paging DB, worker checkpoint + migration additive chưa apply.

---

## 14. L4 — Speaking assessment phía server

| Trước | Sau |
| --- | --- |
| Progress nhận điểm client | `assessmentId` bắt buộc; thiếu → **409** `assessment_required`; điểm client bị bỏ qua |
| Web Speech có thể lưu (trước L1) / preview (L1) | PCM WAV → `POST /api/speaking/{id}/assess` → commit progress; Web Speech vẫn preview-only |
| Không ASR adapter | `WhisperHttpTranscriber` + `SpeechProcessingOptions` (`Learning:Speech`); BaseUrl trống → **503** `engine_not_ready` |
| — | Entity `SpeakingAssessment`; unique `(UserId,SentenceId)` trên progress |

Docs: `docs/operations/local-speech.md`. JS: `speech-recorder.js`. Migration gộp L4–L6: `20261029181904_LearningReliabilityL4ThroughL6`.

**Residual:** whisper.cpp **chưa** cài/spike latency trên máy này; browser mic HTTPS thật chưa chạy; fluency/prosody unavailable.

---

## 15. L5 — Import Speaking captions-only + job

| Trước | Sau |
| --- | --- |
| `CreateOwnedVideo` qua `GetTranscriptForSpeakingImportAsync` (có Gemini) | Dùng `GetEnglishCaptionsOnlyAsync` — **không** Gemini trên đường learner |
| Listening/Admin | Vẫn dùng import cũ có thể Gemini |
| — | `SpeakingImportJob` + worker + `POST /api/speaking/imports` (202) + status GET owner-scoped |
| — | `SpeakingSubtitleParser` SRT/WebVTT (reject invalid timestamps) |

Docs: `docs/operations/speaking-import.md`.

**Residual:** ASR-for-import chưa bật; job phụ thuộc captions; YouTube iframe vẫn cần mạng.

---

## 16. L6 — Completion credit + outbox

| Trước | Sau |
| --- | --- |
| Speaking/Writing completion gọi Goals trực tiếp | `CompletionCreditService` + unique `CreditKey`; outbox cùng transaction |
| Thông báo có thể mất/đúp khi lỗi reward | `LearningNotificationOutbox` + `LearningOutboxDispatcher` drain ≤100/phút trong `NotificationWorker` |

**Residual:** SQLite không chứng minh race MySQL; fault-injection giữa network inference không áp dụng (inference ngoài tx); Goals Vocabulary/Reading/Listening chưa chuyển credit path (đúng plan).

---

## 17. L7 — Grammar / local LLM (mặc định off)

Adapters: `LanguageToolGrammarChecker`, `OllamaWritingModel`; options `Learning:WritingEngines`; **default disabled**. Contract test xác nhận unavailable không chặn core. Docs: `docs/operations/local-writing-engines.md`.

**Residual:** Không cài Java/LanguageTool/Ollama/model; chưa corpus 40 câu chất lượng.

---

## 18. Kiểm chứng L4–L7

```text
dotnet test ... (Speaking|Writing|Notification|Goals|Streak|Sprint1Smoke|Completion|OptionalEngine|SpeakingImport|SpeakingAssessment)
→ 163 passed / 0 failed (~33 s)

node --test scripts/tests/test_speaking_behavior.cjs scripts/tests/test_notification_behavior.cjs scripts/tests/test_speech_recorder.cjs
→ 12 passed / 0 failed
```

Migrations trong repo (chưa apply): `LearningNotificationReliability`, `LearningReliabilityL4ThroughL6`.

**Kết luận chuỗi L0–L7:** Code/core contracts đã vào repo; vận hành ASR/DB apply/engine vẫn cần chủ nhóm. Chưa commit/push.
