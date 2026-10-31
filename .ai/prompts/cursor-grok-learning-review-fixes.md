# Cursor / Grok 4.7 High — Sửa lỗi sau review Learning Reliability

Bạn là agent đang làm việc trong TCT English. Model chủ nhóm muốn dùng là **Grok 4.7 High** trong Cursor; không gọi API Grok từ ứng dụng, không tự chọn model khác. Một agent thực hiện điều tra, code, review và verification; không subagent, worker/reviewer riêng, chat hoặc CLI AI khác.

## Tài liệu phải đọc trước

1. `AGENTS.md`, `.ai/README.md`.
2. `docs/feature-audits/2026-10-30-cursor-learning-implementation-review.md` — bằng chứng R01–R08.
3. `docs/superpowers/plans/2026-10-30-learning-review-fixes-plan.md` — plan sửa **F0–F9**, quyết định và test cụ thể.
4. `docs/feature-audits/bao-chau-learning-execution-ledger.md`; plan gốc L0–L7 chỉ để hiểu lịch sử, không để dòng “đã hoàn tất” thay cho review mới.
5. `docs/project-structure.md`, `.ai/context/coding-conventions.md`, `.ai/context/known-issues.md`, newest entries của `.ai/context/bug-fix-log.md`.
6. Chỉ skill TCT liên quan: `tct-superpowers`, `tct-engineering`, `tct-speaking-feature`, `tct-api-endpoint`, `tct-ef-migration`; đọc DESIGN.md khi sửa trạng thái/form của view. Không load mọi skill hoặc cài lại toolkit.

## Chế độ làm việc và quyền

Thông điệp chủ nhóm đi kèm giao sửa toàn bộ F0–F9 là quyền triển khai trong phạm vi plan, **không phải chỉ phân tích**. Sau khi đã được giao, làm F0 rồi tự tiếp tục F1–F9 theo dependency, cập nhật tiến độ và kiểm thử mỗi bước. Không dừng xin duyệt lại từng bước, không dừng ở việc viết thêm plan.

Plan/prompt này không tự cấp quyền nếu chỉ được mở để đọc. Quyền sửa Program.cs, model/DbContext và migration phải bám thông điệp chủ nhóm cùng mục 1 của plan. Không mở rộng quyền đó sang appsettings/.csproj, package/hạ tầng hoặc chức năng khác.

Nếu một phần thiếu môi trường/ngoài quyền, hoàn tất phần độc lập có thể review, ghi blocker chính xác rồi tiếp tục bước độc lập khác. Không bỏ dở toàn bộ chỉ vì chưa có whisper.cpp/MySQL test server. Không tự triển khai hạ tầng hoặc báo engine đã chạy khi chỉ test stub.

## Phạm vi được giao theo plan

- Sửa Speaking/Writing người học, Goals/Notifications, các service/model/worker/tests trực tiếp giải quyết R01–R08.
- `Program.cs`: chỉ DI/lifetime/options cho các thành phần sửa trong plan.
- Model/DbContext/migration bổ sung: chỉ metadata outbox/checkpoint/job idempotency cần thiết, tạo/review trong repo; không apply DB thật, không sửa/xóa migration cũ, không xóa dữ liệu để làm unique index qua.
- Test bị treo: được sửa phối hợp/lifecycle/deadline trong `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` và test infrastructure có nguyên nhân đã xác minh. **Không sửa AI Chat production**.
- View/JS chỉ bổ sung trạng thái nghiệp vụ/import/phụ đề/feedback cần thiết; giữ UI và aria đang được Antigravity làm, không redesign.

Không đụng Auth/Profile/Settings, Contact, Admin, Billing, AI Chat, Vocabulary/Set/Folder/Class/Chat hoặc Reading/Listening. Chỉ đọc caller/chạy regression để giữ tương thích. Không sửa skills/rules/hooks/MCP hay nâng package/framework.

Không commit/push/PR/merge/deploy. Không reset/stash/clean/checkout để bỏ diff. Không tải/cài engine/model, mở cổng public, đổi key, gọi dịch vụ trả phí hoặc gửi bài/audio thật ra ngoài. Không chạy app trên DB thật do startup seeding; không auto index/sync CodeGraph.

## Những sự thật phải giữ trong khi sửa

- Source target **net9.0 / EF9.0.4 / Pomelo MySQL**, không nâng lên net10/SQLServer theo tài liệu cũ hoặc SDK máy.
- Baseline review ở `feature/bao-chau-ui-audit`, HEAD `3d27a68c8468207eca5a11653c72e735117fc637`, có 47 tracked files modified và nhiều source/test/migration untracked. Chụp baseline mới, không coi toàn bộ diff là code mình viết.
- R01 đã tái hiện: completion trước credit, DB fail bị nuốt, replay không phục hồi thưởng. Transaction phải bao cả completion/effects, không chỉ thêm một transaction trong credit service.
- R02 đã tái hiện: auto-stop làm mất WAV; dispose lúc permission pending vẫn có thể mở mic muộn. Test vòng đời và blob/request thật, không chỉ exports.
- R03 đã tái hiện: 201 user chỉ xử lý 200 rồi succeeded. Phải checkpoint/batch đến hết, không nâng hằng số 200 lên số lớn để né lỗi.
- R04: semaphore trong typed client riêng không giới hạn tổng tải. Limiter phải chia sẻ state giữa request/DI scope và queue có giới hạn thực.
- R05: tạo video và cập nhật job chưa atomic; restart có thể làm job thất bại dù video đã tạo. Unique video không tự giải quyết job recovery.
- R06 đã tái hiện: mọi completion biến thành GoalCompleted và timestamp tương lai. Outbox cần typed intent/snapshot/time thật, không đoán goal từ một event generic.
- R07: adapter/DI chưa có caller. Enabled phải được test từ endpoint Writing; mặc định off và không đổi quyết định passed/XP của Local.
- R08 đã tái hiện: catch filter timeout/cancel bị đảo. Internal timeout → unavailable; caller cancel → propagate.
- ASR transcript/text-match không phải điểm phoneme/fluency/prosody chuẩn. Không tạo điểm random/cố định hay mở lại score-only endpoint để “chữa cháy”.

## Thực thi theo plan

| Bước | Công việc |
| --- | --- |
| F0 | Baseline/contract/ledger, xác nhận lỗi còn tồn tại bằng source hiện tại |
| F1 | Atomic progress/completion/credit/outbox; R01 và R06; fault-injection qua endpoint |
| F2 | Recorder finish-once, giữ WAV auto/manual stop, cleanup/cancel/late permission |
| F3 | Notification worker keyset/checkpoint/conditional lease, >200 user và restart |
| F4 | ASR singleton admission limiter + queue/timeout/cancel/429 |
| F5 | Import atomic job/result, UI 202/poll và flow phụ đề thật, giữ pipeline đồng đội |
| F6 | Adapter Writing timeout/cancel/HTTP/JSON contract |
| F7 | Grammar/local LLM feedback wiring có nguồn; default off, không đổi pass/XP |
| F8 | Test AI conflict đồng bộ đúng, điều tra SQLite flake, full suite không abort |
| F9 | Diff review, docs/bug memory/ledger và ma trận chứng cứ R01–R08 |

Chi tiết files, contract, giới hạn số và acceptance test nằm trong plan; đọc đầy đủ từng bước trước sửa. F5 chưa mở ASR-for-import; F7 chưa thay engine sinh bài song ngữ. Ghi phần đó Deferred, không giả vờ đã có để đóng L5/L7.

## Kiểm thử và tiêu chí hoàn thành

1. Chuyển bằng chứng lỗi thành **regression kỳ vọng hành vi đúng**. Các `.tmp/learning-cursor-review/ReviewProbeTests.cs` từng assert lỗi đang có; pass của chúng không chứng minh fix. Không cần artifact ignored để làm việc vì plan đã ghi đủ kịch bản.
2. Viết regression đỏ, triển khai tối thiểu, chạy xanh; giữ assertion CSRF/IDOR/Premium/5 GoalArea/replay/concurrency. Test nguồn điểm server và nguồn notification đúng nghĩa.
3. Không skip/delete test, đổi expected theo code lỗi, `--test-force-exit`, tăng timeout vô hạn hoặc tắt parallel toàn suite để che lỗi. Test concurrency phải dùng nhiều request/DbContext/DI scopes thật theo mục tiêu.
4. Network/inference ngoài transaction; mọi I/O mới hỗ trợ cancellation. Sau rollback không reuse context lỗi hoặc đánh dấu success giả.
5. Review diff của mình so baseline dirty. Không stage tất cả, không đụng phần UI có sẵn. Khi sửa API giữ route/JSON/selector hiện tại và mô tả field bổ sung rõ ràng.
6. Chạy build, Node regressions, .NET focused, ca từng treo riêng rồi full suite theo mục 14 của plan. Đọc exit code và TRX outcome; 555 pass rồi abort là **chưa hoàn tất**. Ghi cả lần fail/flake và retry.

Lệnh build đúng tên solution:

```powershell
dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln --no-restore --verbosity minimal
```

Lưu artifact dưới `.tmp/learning-review-fixes/`. Khi kiểm suite cuối không import observational probes tạm. Chưa chạy browser/mic/MySQL/engine thật thì ghi Not run/Blocked, không lấy SQLite/stub/screenshot thay bằng chứng đó.

Tạo/cập nhật `docs/feature-audits/bao-chau-learning-remediation-ledger.md`, giữ lịch sử ledger cũ và chỉnh tổng trạng thái cho đúng. Mỗi finding cần status code/test/engine/DB, regression + command/TRX, file sửa và residual. Append bug memory chỉ cho bug đã được sửa/kiểm chứng.

Hoàn tất phần được giao rồi báo tiếng Việt: R01–R08 đã sửa đến đâu, test thật, file đổi, migration chưa apply, engine chưa triển khai, full suite còn blocker gì. Không kết luận mọi thứ production-ready khi chưa vượt các cổng vận hành. Không tự commit/push/merge.
