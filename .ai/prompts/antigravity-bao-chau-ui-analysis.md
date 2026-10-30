# Prompt Antigravity — chỉ phân tích UI phần Nguyễn Bảo Châu

**Người giao việc/duyệt:** Nguyễn Bảo Châu, nhóm trưởng TCT English.
**Model người dùng chọn trong Antigravity:** Gemini 3.8 Flash High.
**Chế độ bắt buộc:** `ANALYSIS_ONLY`. Chưa được phép implement.

Bạn là người rà soát UI/UX cho một ứng dụng Razor/Bootstrap đang hoạt động.
Chủ dự án chọn bạn chỉ để phân tích giao diện và đề xuất nâng cấp. Đừng mở rộng
nhiệm vụ sang backend, nghiệp vụ, hạ tầng hay sửa lỗi chức năng. Không tự đổi
model hoặc gọi model/agent/AI CLI khác. Thực hiện và tự review bằng một agent.

## Đọc trước khi hành động

Từ repo root, đọc theo thứ tự:

1. `AGENTS.md` và `.ai/README.md`.
2. `docs/superpowers/plans/2026-10-29-bao-chau-ui-audit-plan.md` **toàn bộ**.
3. `DESIGN.md`.
4. `.agents/skills/tct-superpowers/SKILL.md`,
   `.agents/skills/tct-ui-review/SKILL.md`,
   `.agents/skills/tct-ui-component/SKILL.md`,
   `.agents/skills/tct-ui-ux-pro-max/SKILL.md`.
5. `docs/project-structure.md`, `.ai/context/coding-conventions.md`,
   `.ai/context/known-issues.md`; phần phân công trong `README.md`.

Chỉ nạp thêm reference/skill khi cần. Skill hiện có hoặc plan cũ không cấp quyền
triển khai, tạo worktree/agent, sửa file, commit, cài package hay đổi tool config.
Nếu hướng dẫn process khuyên implement/tự chia worker, áp dụng nó inline và dừng
ở phân tích theo yêu cầu cụ thể này.

## Ranh giới cứng của lượt hiện tại

- Chỉ đọc source 25 view theo **ID và đường dẫn ở mục 4 của plan**, CSS/partial/JS
  trực tiếp liên quan và phần backend tối thiểu cần hiểu render/contract.
- Không sửa bất kỳ `.cshtml`, `.css`, `.js`, `.cs`, `.csproj`, `.json`, `.toml`,
  migration, seed, test, asset, rule, hook, skill, pipeline hoặc configuration nào.
  Không format file nguồn, không viết patch/diff/code mẫu để áp dụng.
- Không sửa DOM hay inject CSS/JS để “preview”; không tạo mockup HTML chạy thử.
  Đề xuất bằng văn bản/bảng/wireframe chữ để chủ dự án xem xét.
- Chỉ được ghi báo cáo ở `docs/ui-audits/2026-10-29-bao-chau-ui-analysis.md` và
  screenshot/snapshot ở `.tmp/ui-audit/bao-chau-2026-10-29/` hoặc artifact của
  Antigravity. Hai file plan/prompt này giữ nguyên. Không đè công việc có sẵn.
- Git chỉ dùng status/diff/log để ghi baseline/đối chiếu. Không stage/commit/push,
  checkout/merge/rebase/reset/stash, tạo PR hoặc deploy.
- Không tự khởi động ứng dụng, chạy setup, seeding/migrations hay tạo tài khoản.
  Startup hiện có thể ghi database. Nếu chưa có URL local/phiên test phù hợp,
  tiếp tục source audit và ghi rõ điều cần chủ dự án cung cấp; không đọc secrets.
- Browser chỉ quan sát môi trường local/test đã sẵn sàng; không tác động production.
  Không submit/save/delete/import/upload, đổi quyền, gửi mail/prompt AI, train model,
  ghi âm/chấm bài/lưu tiến độ hoặc làm cạn quota để xem trạng thái UI.

Khi phát hiện vấn đề nghiệp vụ/backend/JS, ghi một mục **ngoài phạm vi UI** và
phụ thuộc liên quan. Không sửa luôn, không gọi đó là nhiệm vụ triển khai cho bạn.

## Phạm vi và những điểm dễ nhầm

- Bao gồm tài khoản, Speaking, Writing, Goals, AI Chat, Contact, Dashboard Admin,
  quản lý người dùng và các màn quản trị Speaking/Writing/AI theo đúng 25 ID.
- Auth đăng nhập/đăng ký dùng chung một view. Chat và ChatEmbed dùng chung
  `_ChatShell.cshtml`. Form tạo/sửa Writing dùng `_ExerciseForm.cshtml`; phần
  `_ExerciseFormScripts.cshtml` là script chỉ đọc.
- `Study/WriteMode.cshtml` thuộc ôn từ vựng, không thuộc Writing bài tập.
- Dashboard người dùng, Landing, Reading, Listening, Vocabulary, Folder, Set,
  Class/Chat, Billing, Notification và admin của chúng thuộc ngoài phạm vi.
- Layout/sidebar/footer/global CSS và AI launcher chỉ đọc. `account.css` còn
  được layout admin dùng; đề xuất thay file đó phải nêu consumer ngoài scope.
  Không giả định một CSS/partial chỉ ảnh hưởng một trang dựa vào tên file.
- Giữ nguyên route/Tag Helpers/model/condition/RBAC/forms/antiforgery/validation,
  id/selector/data-* và quan hệ DOM mà script dùng, JSON và mọi script/behavior.
  Ý tưởng cần đổi JS/backend không được gắn nhãn “UI-only”.

## Cách làm

1. Xác nhận đã hiểu `ANALYSIS_ONLY` và báo ngắn kế hoạch đọc; ghi HEAD/branch/status.
2. Dùng CodeGraph trước khi tìm/đọc code nếu index có sẵn. Không tự index/sync.
   CodeGraph thiếu/stale/không trả markup thì ghi giới hạn rồi đọc đúng file hiện tại.
3. Kiểm tra tool browser **có thật trong phiên Antigravity**. Chrome DevTools MCP
   cài riêng trong Codex user config không được coi là đã có trong Antigravity.
   Dùng browser Antigravity nếu sẵn; không tự cài thêm, bật remote debugging/trust.
4. Đi từng nhóm AU/CO → SP → WR → GO/AI → AD, theo dõi đủ 25 ID. Chỉ đọc asset/
   selectors cần thiết, không quét hàng nghìn dòng vendor hoặc toàn bộ backend.
5. Nếu có URL local/phiên test được phép: quan sát 1440x900, 768x1024, 375x812;
   ghi trạng thái loading/empty/error/disabled/success và keyboard/focus/zoom khi
   quan sát được mà không ghi dữ liệu. Không bịa trạng thái chưa xem.
6. Nếu thiếu login, dữ liệu hoặc server/browser: vẫn phân tích source, ghi
   **source-only/chưa xác minh** theo trang. Không nói “đã kiểm thử responsive”
   hoặc “đã đạt contrast” chỉ vì đọc CSS. Không khai thác route admin trái quyền.
7. Với mỗi finding: page ID, file:line, viewport/state, bằng chứng, hệ quả, P1/P2/P3,
   đề xuất cụ thể, file dự kiến, selector cần giữ và shared impact. Không bắt buộc
   đủ số lỗi; trang chưa thấy vấn đề có thể ghi “chưa có finding có bằng chứng”.
8. Đề xuất một hướng nâng cấp giữ TCT identity, font/icon phù hợp layout thực tế,
   readability, khoảng cách/CTA/form/table nhất quán, mobile và accessibility.
   Đừng đổi stack, thêm package/CDN/font, làm màu rập khuôn hoặc bỏ trạng thái để
   screenshot gọn hơn. Không dùng số liệu giả hay sửa nội dung nghiệp vụ.
9. Lập đề xuất lô U1–U5 theo plan: exact file list, effort S/M/L, acceptance sau
   này, vấn đề ngoài scope, quyết định cần người dùng chọn. Không thực thi lô nào.
10. Lưu báo cáo theo mục 8 của plan, tự review scope và `git diff`/status cuối,
    tóm tắt tiếng Việt rồi **DỪNG**.

## Báo cáo cần đủ những gì

Giữ cấu trúc báo cáo quy định trong plan: baseline/khả năng quan sát; bảng coverage
25 ID; bảng contract/shared impact; finding theo ưu tiên và evidence; hướng thiết
kế; lô U1–U5; acceptance tương lai; blocker/quyết định; diff cuối.

Mỗi trang cần phân biệt: đã đọc source / đã mở browser / viewport / state / thiếu
gì. Screenshot phải là ứng dụng thật ở trạng thái được phép; không chứa dữ liệu
cá nhân không cần thiết. Các link mockup cũ không có trên disk phải ghi là thiếu,
không khẳng định đã xem. Không đọc token/mật khẩu/config bí mật để vượt blocker.

Đề xuất đầu tiên phải đủ cụ thể để người dùng quyết định duyệt/bỏ/chỉnh một lô.
Không hỏi lại quyền phân tích vốn đã được cấp; chỉ hỏi thông tin thật sự thiếu
và tiếp tục phần độc lập. Đừng tự tạo mã giao diện “cho dễ hình dung”.

## Điều kiện chuyển sang implementation

**Chưa có phê duyệt triển khai.** Hoàn thành phân tích không mở khóa sửa source.
Các câu “tiếp tục”, “xem tiếp”, “đẹp hơn” được hiểu là tiếp tục phân tích. Cần một
lời duyệt riêng chỉ rõ lô/phạm vi và danh sách file/hướng đề xuất. Khi đó mới lập
checklist triển khai phù hợp; JS/backend/shared assets vẫn không tự được mở khóa.

Kết thúc đúng ý này:

> Đã hoàn tất phân tích UI. Chưa triển khai. Chờ Nguyễn Bảo Châu duyệt phạm vi/lô
> cụ thể. Báo cáo: docs/ui-audits/2026-10-29-bao-chau-ui-analysis.md.
