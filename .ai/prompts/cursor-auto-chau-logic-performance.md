# Prompt thực thi Plan2 — Cursor Auto

Bạn là agent duy nhất triển khai logic/performance cho TCT English. Owner giao
thực thi Plan2 trên local theo checklist chi tiết, không dừng ở việc tóm tắt plan.
Cursor Auto được chọn model trong allowlist của owner; không đổi IDE settings.
Model web giữ OpenAI `gpt-6-luna`/reasoning `low`, audio Whisper.

## Bắt đầu và thứ tự

Review02/11 đã xác minh5finding UI cũ đóng. Trước BE0, thực hiện preflight
`docs/superpowers/plans/2026-11-02-plan2-handoff-preflight.md`: breakpoint Admin992px
đã có từ baseline và behavior tests cho submit guard. Ghi vào ledger Plan2; không
mở lại toàn Plan1. Prompt tổng hợp: `.ai/prompts/cursor-plan2-handoff-preflight.md`.

1. Đọc AGENTS.md; plan
   `docs/superpowers/plans/2026-11-02-chau-logic-performance-plan.md`; audit
   `docs/feature-audits/2026-11-02-chau-ui-performance-audit.md`; ledger Plan1
   `docs/feature-audits/2026-11-02-chau-ui-execution-ledger.md` nếu đã có.
2. Branch/status/dirty diff. Nhánh gốc `feature/chau-ui-performance-audit`, master
   baseline210bf2d. Owner sẽ giao prompt này sau review UI. Nếu UI ledger thiếu,
   xác minh chỉ dẫn mới; có thể làm backend độc lập mà không bịa UI đã pass.
   Giữ diff/tài liệu UI và BTH2; không checkout/reset/stash hoặc overwrite agent khác.
3. Load tct-superpowers+tct-engineering, bug memory phù hợp; API/security/admin/
   speaking/EF skills khi active batch cần. CodeGraph trước, không full index.
4. Tạo/cập nhật `docs/feature-audits/2026-11-02-chau-logic-performance-ledger.md`.
   Thực hiện **BE0 → BE1 → BE2 → BE3 → BE4 → BE5 → BE6 → BE7 → BE8**.

Làm từng batch nhỏ, regression red trước fix khi test mới được thêm, xác minh
green, review diff và ghi checkpoint rồi tiếp tục. Không nhảy sang tối ưu query
khi Account/Chat correctness còn regression fail. Nếu context đầy, ledger ghi
step dở/files/commands/next; phiên sau tiếp tục ở đó, không làm lại từ đầu.

## Ưu tiên phải hiểu đúng

- L01: Login BCrypt.Verify trên SOCIAL_LOGIN_... ném SaltParseException. Test
  endpoint bằng synthetic user trước guard; không phá đăng nhập hợp lệ/OAuth.
- L02: cancel sau save user prompt không qua catch AiProviderException nên có
  thể còn message/draft. Cleanup bounded và lease-safe, original ct đã canceled.
- L11: Stop không có activeStreamId lúc provider chờ, vì AiStreamStarted chỉ
  sau generate xong. Kiểm cả hub scheduling và HTTP fallback. Không giả 'đã dừng'
  khi chỉ ngừng animation; không fallback tự sinh lượt AI thứ2 cho cùng intent.
- L03: quota count-before-call +guard theo conversation có race across conversations.
  Đây là risk source, cần test barrier/fake provider/MariaDB để chứng minh trước.
  Nếu đúng: DB-authoritative reservation atomic, không static lock rồi claim multi-instance.
- L04–06: server paging/bounded history/projection thật. Không materialize toàn
  DB rồi Skip/Take; không xóa history/attempts hoặc làm sai filter/progress/resume.
- L07–09: đo query render, SMTP bounded/cancel/result semantics, assets pin và
  diagnostic an toàn; không bật worker mặc định hay thêm cache private dùng chung.

Phải giữ5GoalAreas, receipt/credit/transaction/outbox, IDOR404, role/quota hợp
đồng, headers/antiforgery, routes cũ và UI Plan1. AI có cả SignalR và HTTP fallback;
không nhầm class chat upload với AI chat. Không rewrite ClassChatHub nghiệp vụ lớp.

## Quyền thực thi có giới hạn

Được sửa services/controllers/ViewModels/tests và JS integration cần thiết ở
plan; Program.cs chỉ DI/flags cần cho scope, settings không secret. Không nâng
framework/packages hoặc .csproj tùy tiện. Cho phép additive persisted model/
index migration cần cho BE2/BE4 SAU regression/measurement và design trong ledger.

Mọi schema change: dùng tct-ef-migration/repo-local EF, gửi migration+Designer+
snapshot cùng diff, review Up/Down/index-FK ordering, migrate/repeat/preservation
trên MariaDB10.5.23 disposable đúng identity, pending-model check sạch. Không
edit applied migrations/history, DROP, reset dev DB hoặc update hosting.

Không chép data/key/SQL backup/native binary/trust vào Git. Owner cho tạo account/
fixtures và test local riêng; không thao tác user/content thật. Không credentials
trong chat/log/argv. Không paid API/live SMTP/YouTube trong regression mặc định.
Không Task.Run fire-and-forget, .Result/.Wait hoặc Task.WhenAll cùng DbContext.

Một agent inline cả implement/review; không subagent/AI CLI/another model process.
Không commit/push/PR/CI remote/merge/deploy/Plesk; gate phải giữ false. CI xanh
không có nghĩa được release. Không bật AutoUnlockWorker production để test.

## Verification và giới hạn

Local web dùng `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`; nếu
listener bận giữ workload, chọn free Release port. Xác nhận health, Speech Status,
key source, không dump secrets. Không auto-start Whisper từ hooks/build/folderOpen.

Đo before-after cùng fixture/environment: warm-up5, sample>=30 cho scenario
nặng; report query count/rows/bytes/allocations/latency +data scale. Không gọi
single loopback GET là hosting benchmark hoặc sao estimated savings thành kết quả.
Không tối ưu bằng arbitrary caps làm mất UI/API semantics; có Load older/page thật.

Chạy focused tests sau changes, full app suite cuối; MariaDB và ASR opt-in chỉ
khi prerequisite/identity đúng, không sửa guards. Dùng fake provider để cancel,
quota, retry; không dựa cloud để test deterministic behavior. Test schema trên
provider đúng, SQLite không thay MariaDB concurrency. Tooling sửa thì chạy checks
AGENTS quy định; không full-build app trên mọi hook/edit.

Nghiệm thu browser Standard/Premium/Admin, Plan1 mobile/keyboard không regress.
Clean-PC/mic/accent/noise/YouTube/staging recovery chưa chạy phải ghi đúng nhãn;
không dừng toàn task vì một gate ngoài môi trường, tiếp tục phần độc lập và nêu
blocker có chứng cứ. Không deploy để biến gate đó thành pass.

## Kết thúc

Ledger chi tiết red→green/commands/results, model/schema/perf tradeoff, files,
bugs đã sửa/known-issues được cập nhật, acceptance còn thiếu. Kết quả local riêng
với CI/production. Dừng local để owner review; không tự làm remote stages.
Không gọi pronunciation/prosody engine hoặc semantic Writing nâng cao là đã
xây xong: hai nâng cấp sản phẩm đó không nằm trong Plan2.

Đoạn chat ngắn để owner dùng:

> Plan1 đã được review. Thực thi `.ai/prompts/cursor-auto-chau-logic-performance.md`
> trên nhánh hiện tại, giữ toàn bộ UI đã làm. Làm BE0–BE8 theo batch, ưu tiên
> Account/Chat correctness rồi mới tối ưu, có đo trước-sau và regression tests.
> Chỉ local; không commit/push/CI remote/merge/deploy. Xong ghi ledger để tôi review.
