# Cursor / Grok 4.7 High — Sửa Learning Reliability sau review vòng 2

Bạn làm trong repo TCT English. Model theo lựa chọn chủ nhóm là Grok 4.7 High trong Cursor; không gọi API model từ ứng dụng. **Một agent** tự điều tra, code, review và verification; không subagent, reviewer riêng hoặc chat/CLI AI khác.

## Nạp ngữ cảnh theo bước, không đọc cả bộ tài liệu

1. Tái sử dụng `AGENTS.md`/`.ai/README.md` hiện có trong chat; chỉ đọc nếu chưa có hoặc đã đổi.
2. Mở mục 1–4 của `docs/superpowers/plans/2026-10-30-learning-remediation-round2-plan.md` để biết scope/quyền/thứ tự, rồi chỉ đọc chi tiết bước C đang làm. Hoàn thành toàn bộ C0–C9 theo dependency, không nạp mọi bước/source cùng lúc.
3. Khi cần bằng chứng lỗi, đọc đúng RR tương ứng trong `docs/feature-audits/2026-10-30-learning-remediation-rereview.md`; ledger chỉ đọc hàng trạng thái cần biết. Plan F0–F9 cũ và ledger lịch sử chỉ tra cứu nếu contract còn thiếu, không bắt buộc đọc đầu phiên.
4. Chỉ mở skill cho phần hiện tại; project structure/conventions/known issues/bug log chỉ lấy section liên quan. Không đọc mọi role, adapter hoặc thư mục vendor.

CodeGraph trước cho source đã index: query exact symbol, khởi đầu maxFiles 1–3, tái sử dụng source verbatim đã trả. File stale/chưa index hoặc thiếu đoạn cần sửa thì đọc trực tiếp có giới hạn và nêu lý do; không lặp lại câu hỏi rộng rồi đọc cả file. Context7 nếu thực sự có tool: chỉ tra API/library chưa chắc chắn, đúng version net9/EF9/Pomelo; resolve một lần, tái sử dụng library ID. Nó không thay source dự án; không gọi cho mọi file và không gửi code/private logs. Thiếu tool thì báo một lần, dùng tài liệu chính thức khi cần. Không cài thêm server hoặc sync index tự động.

## Quyền và cách tiến hành

Thông điệp chủ nhóm duyệt C0–C9 kèm prompt này là quyền **triển khai**, không chỉ phân tích. Chụp baseline C0 rồi tự tiếp tục từng bước, viết regression đỏ, sửa, chạy xanh, cập nhật ledger. Không chỉ trả thêm một plan; không dừng xin duyệt lại từng bước đã được giao.

Chỉ mở/đọc prompt không tự cấp quyền. Khi có thông điệp duyệt, phạm vi đặc biệt là:

- `Program.cs`: DI/lifetime/options của các phần Learning trong plan nếu thật sự cần.
- Model/DbContext và migration bổ sung trong repo: metadata/checkpoint/provenance cần thiết; ưu tiên cột đã có. **Không apply database thật**, không sửa/xóa ba migration hiện tại, không DROP dữ liệu cũ.
- AI conflict test/test infrastructure: đồng bộ, gate, deadline, SQLite lifetime có bằng chứng; **không sửa production AI Chat**.
- View/JS: form import/phụ đề, trạng thái thu/chấm/feedback, giữ UI hiện có; không redesign.

Không sửa `.csproj`, `appsettings*`, package/framework, rules/skills/hooks/MCP, Auth/Profile/Contact/Admin/Billing/AI production/domain khác. Không reset/stash/clean bỏ diff, không commit/push/PR/merge/deploy, không chạy app trên DB thật vì startup seeding. Không cài engine/model/hạ tầng, dùng key/audio thật hoặc auto index/sync CodeGraph. Target đúng net9.0/EF9.0.4/Pomelo MySQL, không nâng theo SDK hay tài liệu cũ.

Thiếu MySQL/browser/whisper/LanguageTool/Ollama thật thì ghi Not run/Blocked và tiếp tục code/test độc lập bằng stub. Không dùng việc thiếu môi trường để bỏ UI/polling/atomic claim/regression trong quyền.

## Trạng thái phải hiểu đúng

- Credit rollback/retry Speaking, recorder finish-once, keyset qua user 201, singleton limiter và typed outbox là tiến bộ đã được chứng minh; bảo toàn chúng.
- RR01 mới: failed/cancelled job tạo lại cùng unique fingerprint nên lỗi DB. Giữ unique, sửa retry/idempotency/concurrency; succeeded nhưng video đã xóa phải xử lý.
- RR02: duplicate trong notification batch làm detach phần chưa gửi rồi tăng cursor; probe có 8/201, thiếu 193 nhưng succeeded. Hành vi đúng phải đủ người nhận, không assert thiếu là pass. Phase/LastUserId/LeaseToken có trong schema chưa đồng nghĩa worker sử dụng.
- RR03: recorder riêng dispose đúng nhưng caller Speaking không gọi dispose khi đổi câu; PCM còn mở, callback HTTP còn stale, auto-stop chưa reset state trang, mic denied còn unhandled promise.
- RR04: UI còn POST đồng bộ `/Speaking/My/Create`; job polling và subtitle upload chưa có đường dùng thực tế. Có parser/cột DB không thay cho flow.
- RR05: timeout bắt đầu sau acquire; queue chưa thuộc deadline. Một CTS tổng trước queue+HTTP, giữ singleton cap đúng process.
- RR06: `Streak == LongestStreak` không chứng minh record mới. Dùng transition metadata và badge IDs vừa award, không cửa sổ 2 phút.
- RR07: cloud feedback chạy trước CTS 3 giây của grammar/model; tổng budget chưa đúng. Một deadline chung, validate JSON và mapping feedback có nguồn, giữ Local pass/XP.
- F8 chưa sửa: ca AI conflict còn Task.Delay(50), gate thiếu finally/deadline/cancel. SQLite fixture còn init/dispose flake. 558 pass trước abort vẫn là full suite chưa hoàn tất; ca AI chạy riêng pass không đóng vấn đề trong suite.

## Checklist thực thi

| Bước | Công việc |
| --- | --- |
| C0 | Chụp baseline dirty; tái hiện; ledger vòng 2, bắt đầu chẩn đoán fixture |
| C1 | Import failed/cancelled retry, unique conflict, double enqueue, deleted result |
| C2 | Notification batch không mất event; durable phase/cursor, conditional lease/fencing/heartbeat |
| C3 | PCM + HTTP cancel/dispose/epoch, finish-once, state và error promise |
| C4 | ASR shared limiter: queue+inference chung deadline; multi-scope/overflow/cancel tests |
| C5 | Transition-based goal/streak/badge intents; atomic credit/outbox, Writing fault injection |
| C6 | Import atomic publish/job, worker lease/retry; UI 202/poll/resume, multipart SRT/VTT |
| C7 | Writing optional budget tổng 3 giây, schema DTO/feedback nguồn, endpoint spies |
| C8 | AI test gate và SQLite lifetime trong tests; giữ assertion, không che race |
| C9 | Build/Node/focused/isolated/full Completed, review diff, ops/bug log/ledger |

Files, phụ thuộc và tiêu chí chi tiết nằm trong plan. Làm C6 sau C1 và mẫu lease C2; C8 có thể xử lý sớm khi fixture cản regression. Network ngoài transaction; video/sentences/job result phải commit chung, notification batch/cursor không tạo khoảng skip khi crash.

## Kiểm thử và bàn giao bắt buộc

- Test đúng kỳ vọng: đủ 201/450 người nhận, failed job resubmit thành công, đổi câu dừng mic ngay, callback cũ không đổi UI, slot timeout tính queue, chỉ vượt record mới thông báo. Không chuyển defect probe thành test assert hành vi lỗi.
- Test concurrency cần nhiều request/DbContext/connection/DI scopes thực theo mục tiêu; không coi hai tác vụ trên cùng context là kiểm thử đa worker.
- Node phải bơm PCM samples, có Blob thật trong fixture, callback/submit một lần; test phải fail nếu Blob null ở ca có mẫu. Không `--test-force-exit`.
- Không skip/delete test, nới assertion, tăng timeout vô hạn hoặc tắt parallel toàn suite để xanh. Root cause fixture phải có bằng chứng; provider stub/gate cleanup có deadline/finally.
- Chạy build và lệnh Node/.NET trong C9; lưu TRX dưới `.tmp/learning-remediation-round2/`. Không import `.tmp/.../ReviewProbes.targets` vào suite cuối, không phụ thuộc artifact reviewer trên máy khác.
- Báo cả fail/flake/abort và retry. Full suite chỉ qua khi **Completed, exit 0**. Giữ engine/MySQL/browser/corpus Not run nếu chưa kiểm; ASR-import Deferred; sinh bài song ngữ vẫn cloud.
- Tạo `docs/feature-audits/bao-chau-learning-remediation-round2-ledger.md`; giữ report/ledger cũ. Ghi mỗi finding: source/file, test đỏ/xanh, command/TRX, status code/test/environment, giới hạn còn lại. Bug log chỉ append lỗi đã sửa/kiểm chứng.
- Review diff so baseline, giữ hunk của người khác. Không tự stage/commit/push. Cuối cùng báo tiếng Việt những gì đã làm, bằng chứng và blocker; nếu còn lỗi thì kèm plan, prompt và câu chat cập nhật theo AGENTS.md.

Hoàn thành toàn bộ phần code/test đã được giao, không đánh đồng adapter tồn tại với engine đã vận hành hoặc fields tồn tại với flow đã xong.
