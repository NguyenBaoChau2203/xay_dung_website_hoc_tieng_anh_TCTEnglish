# Gemini3.8 Flash High — sửa findings review Plan1

Owner giao thực thi repair cục bộ Plan1 trên local. Bạn là agent duy nhất, tự
implement/review/verify inline; không subagent/chat/CLI/model worker khác.

Đọc AGENTS.md, DESIGN.md, rồi:

1. `docs/feature-audits/2026-11-02-gemini-plan1-review.md`.
2. `docs/superpowers/plans/2026-11-02-plan1-ui-review-repair.md`.
3. Ledger Plan1 hiện tại, skill tct-superpowers/tct-engineering/tct-ui-review,
   bug memory liên quan; skill UI/domain chỉ đọc khi cần bước hiện hành.

Giữ nhánh `feature/chau-ui-performance-audit` và toàn bộ dirty diff của Plan1,
tài liệu mới và BTH2. Không reset/stash/checkout hoặc làm lại toàn Plan1.
CodeGraph exact symbols trước, không init/full rebuild.

Thực thi R0→R1→R2→R3→R4→R5→R6, ghi checkpoint vào
`docs/feature-audits/2026-11-02-plan1-ui-repair-ledger.md`.

- R1 P1: Enter requestSubmit vượt sendBtn disabled. Chặn in-flight ở submit
  handler trước append/clear/network, không chỉ keyboard handler. Giữ draft đang
  soạn, kiểm cả click/Enter/quick actions và HTTP/SignalR fake. Promise pending
  regression phải chứng minh1call, sau success/error gửi tiếp được.
- R2 P2: chọn ảnh mới đọc thành công nhưng img vẫn display:none từ fallback.
  Sửa state success/error để preview hiện và initials ẩn đúng, không upload thật.
- R3 P2: min-height220 trong wrapper clip gây mất dòng cuối ở390×500 dù scroll
  hết. Sửa flex/scroll thích ứng; kiểm last line visibility full/embed, metrics/
  history open và textarea dài. Không fix bằng overflow hidden che vấn đề.
- R4 P2: native Tab đi vào main sidebar đóng ngoài màn hình; Account/Admin
  aria-expanded không đồng bộ. Sync inert/expanded/focus theo actual breakpoint,
  init/resize/open/close/Escape; không để desktop sidebar bị inert sau rotate.
- R5 P2: skip link Admin z-3 bị sticky navbar che khi focus trên mobile390.
  Sửa focused layer/position, kiểm native Tab/Enter và hit-test, desktop control.
- R6: fresh build+tests và ledger trung thực. Bản101test --no-build trước dùng
  Debug binaries31/10; reviewer đã chạy fresh811pass/8skip. Không dùng số cũ để
  chứng minh sửa mới. Mapping U10 vẫn AccessDenied404 thuộc Plan2.

Mỗi lỗi cần regression red→green meaningful; dùng browser với account fixture
local, fake HTTP/provider/DOM để không gọi OpenAI trả phí hoặc gửi email. Owner
cho phép tạo/test account local riêng; không sửa/xóa user/content thật; cleanup
fixture đúng identity. Key chỉ local, không output/prompt/Git/argv.

Khi cần web: launcher chuẩn scripts/start-local.ps1 -NoBrowser, giữ listener bận
và chọn free Release port. Verify health/Speech Status/key-source metadata, không
auto start ASR từ hook/build. Không kill process theo tên hoặc đổi native trust.

Không sửa backend/Program/appsettings/csproj/migrations, AI model/quota/transport
contracts. Website vẫn gpt-6-luna low, không Gemini API. Stop-before-Started và
server pagination thuộc Plan2, không lấn sang khi sửa submit UI.

Không commit/push/PR/CI remote/merge/deploy/Plesk; gate giữ false. Fullsuite cuối
chạy fresh như repairplan; không fulltest sau mỗi chỉnhCSS hoặc bỏ guard để xanh.
Đánh dấu skipped/unverified riêng, không claim25view/physical keyboard/mic đã pass
khi không có bằng chứng. Append bug log sau fix và cập nhật ledger consistent.

Hoàn tất repair, review diff inline và báo owner xem lại trước khi sang Plan2.
Không dừng ở việc viết lại plan; nếu blocked một môi trường, ghi chứng cứ và
tiếp tục các bước độc lập trong phạm vi repair.

Đoạn chat owner copy:

> Đọc và thực thi `.ai/prompts/gemini-plan1-ui-review-repair.md`.
> Sửa R1–R5 theo repair plan, giữ toàn bộ UI Plan1 đã làm và kiểm chứng bằng
> regression/browser. Đồng bộ ledger ở R6. Chỉ local, không commit/push/CI/deploy
> và chưa làm Plan2. Xong báo tôi review lại.
