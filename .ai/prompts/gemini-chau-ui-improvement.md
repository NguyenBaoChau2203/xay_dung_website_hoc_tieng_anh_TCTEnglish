# Prompt thực thi Plan1 — Gemini3.8 Flash High

Bạn là agent duy nhất thực thi UI cho TCT English. Owner giao **triển khai Plan1
trên local** theo tài liệu dưới, không chỉ phân tích/viết lại plan.
Model IDE được owner chọn Gemini3.8 Flash High; không thay model trong source web.
Website vẫn OpenAI `gpt-6-luna` +reasoning `low`, audio Whisper.

## Bắt đầu

1. Đọc AGENTS.md, DESIGN.md, plan
   `docs/superpowers/plans/2026-11-02-chau-ui-improvement-plan.md` và audit
   `docs/feature-audits/2026-11-02-chau-ui-performance-audit.md`.
2. Đọc tct-superpowers/tct-engineering/tct-ui-review/tct-ui-component; các skill
   khác nạp đúng batch. CodeGraph trước cho source indexed, maxFiles1–3; đọc
   CSS/Razor/JS gap có mục đích. Không init/rebuild index, không full context bundle.
3. Kiểm branch/status. Nhánh owner đã tạo `feature/chau-ui-performance-audit`
   từ master210bf2d. Giữ mọi tài liệu/diff đang có và BTH2 chưa track. Nếu owner
   đã chuyển nhánh mới, theo chỉ dẫn mới; không checkout/reset/stash tự động.
4. Tạo/cập nhật
   `docs/feature-audits/2026-11-02-chau-ui-execution-ledger.md` với baseline,
   checklist UI0–UI7 và checkpoint. Đọc duy nhất bước đang làm và dependency.

## Những việc phải làm

Thực hiện tuần tự toàn Plan1: UI0 baseline → UI1 auth → UI2 navigation → UI3
account → UI4 learning → UI5 chat → UI6 admin → UI7 verification. Sau mỗi batch
kiểm diff, xác minh behavior liên quan, ghi ledger rồi tiếp tục. Không dừng sau
UI0 để hỏi xác nhận chung; nếu một state không thể nghiệm thu, ghi đúng blocker
và tiếp tục phần độc lập. Không ghi pass giả cho state chưa có dữ liệu/role.

Các lỗi có bằng chứng, phải ưu tiên:

- Profile/Settings mobile: account.css1303+ override media680 làm sidebar260px
  nằm trong flex flow, main còn122px ở viewport390. Sửa cascade/scoping thật.
- AI Chat mobile: metrics299px ép log còn25.78px. Tổ chức lại hierarchy,
  disclosure metrics/history, vùng hội thoại dùng được và composer multiline.
- Auth label/input, submit pending/duplicate, overflow393/390 và hero mobile dài.
- Profile75%/40% hardcode: bỏ số giả, CTA/empty honest; không tự phát minh số khác.
- Shell click-only, lang, skip-main; Settings password toggle names/tab order;
  Admin sidebarToggle và IsPublished label; avatar fallback đang404.

Giữ DESIGN.md và style hệ thống; không thay toàn diện thành một template mới.
Không restore random Speaking score, auto-pass Writing, hoặc bỏ server receipts.
Goals vẫn5areas. Full page/embed và HTTP/SignalR phải còn hoạt động.

## Ranh giới được sửa

Razor/CSS/UI JS theo allowlist trong plan. Giữ names/actions/routes/selectors,
antiforgery, JSON, SignalR events và owner/role checks. JS thay đổi chỉ
presentation, accessibility, form state và composer interaction.

Không sửa controllers/services/entities/DbContext/Program.cs/appsettings/.csproj/
migrations, AI model/key/quota/score/cancel backend. L11 Stop-before-Started,
server paging, email, Account validation là dependency Plan2: ghi ledger,
không giả sửa bằng một nhãn UI. Không add unimplemented Load older button.

Một agent thực thi +review inline, không spawn worker/reviewer/chat/CLI/model khác.
Không sửa global IDE settings/native trust. Không dùng upstream skill CLI để vượt
policy dự án. Không tự commit/push/PR/remote CI/merge/deploy hoặc bật Plesk gate.

## Chạy và kiểm tra

`pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` để DB+Whisper+web.
Cổng bận: không kill/reuse listener mù; chọn free port với
`-Configuration Release -WebPort <port>`. Health +Speech Status +key-source
metadata; không in/copy key hoặc coi configured là quota/access đã pass.

Owner cho phép tạo account và thao tác test local riêng. Dùng existing local
fixtures hoặc synthetic account được đánh dấu; không sửa/xóa user/content thật.
Credentials ignored/local, không chat/Git/argv. Không gửi mail thật, gọi paid API,
train ML.NET hay import YouTube chỉ để chụp UI. Mock network/state thay thế khi cần.

Before/after:1440×900,390×844; thêm375/768, zoom200%, keyboard/reduced-motion.
Ghi actual viewport, bỏ false-positive offscreen sidebar đang đóng. Test từng
state của batch bằng browser nếu có; source/HTTP200 không thay visual/keyboard
acceptance. Sử dụng screenshot đã che thông tin cá nhân, lưu ignored .tmp.

Build/test như UI7, không unit-test từng CSS class hoặc full suite sau mỗi pixel.
Không bỏ guard/test để có màu xanh. Khi code JS thay đổi logic trạng thái có rủi
ro, dùng behavior regression có ý nghĩa. Required tooling suite chỉ khi chạm tooling.
Sau fix bug đã verified, ghi bug-fix-log và known issue liên quan theo AGENTS.

## Handoff bắt buộc

Nêu files/behavior/checks actual, closed/partial/deferred, ảnh before-after và
manual acceptance còn thiếu; phần source vsbrowser rõ. Giữ toàn diff local để
owner review rồi mới cho Cursor thực thi Plan2. Không claim sản phẩm/production
hoàn thiện hoặc tự làm tiếp Plan2.

Đoạn chat ngắn để owner dùng:

> Thực thi `.ai/prompts/gemini-chau-ui-improvement.md` trên nhánh hiện tại.
> Làm lần lượt UI0–UI7, dùng các skill/công cụ dự án và browser để kiểm chứng.
> Giữ API/logic/backend; không commit, push, chạy CI remote hoặc deploy.
> Hoàn tất thì ghi ledger và báo tôi review trước khi sang Plan2.
