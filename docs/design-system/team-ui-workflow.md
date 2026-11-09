# Quy trình UI chung cho team — TCT English

Mỗi người tự thiết kế và làm màn hình được giao; **màu, font, khoảng cách, shell và
control dùng chung một bộ chuẩn** để website nhìn thống nhất. Tài liệu này nói về
*cách làm việc*. Giá trị cụ thể nằm ở [DESIGN.md](../../DESIGN.md) (chuẩn) và
[`tct-learning-ui.css`](../../TCTEnglish/wwwroot/css/tct-learning-ui.css) (code). Không
chép lại giá trị ở nơi khác.

> Nền tảng này đã được triển khai **cục bộ** và chưa được commit/đẩy lên GitHub hay
> hosting. Toàn bộ website **chưa** được thiết kế lại: các màn hình cũ vẫn giữ CSS riêng
> cho đến khi chủ màn hình chuyển sang token (mục 6).

## 1. Vị trí file chuẩn và cách dùng

| Cần | Ở đâu |
| --- | --- |
| Chuẩn thiết kế (màu, font, bo góc, lớp, layout, a11y) | `DESIGN.md` |
| Token (`--tct-*`) và class (`.tct-*`, `.tct-theme`) | `TCTEnglish/wwwroot/css/tct-learning-ui.css` |
| Partial dùng chung | `TCTEnglish/Views/Shared/_PageHeader.cshtml`, `_StateMessage.cshtml` |
| ViewModel của partial | `TCTEnglish/ViewModels/SharedUiViewModels.cs` |
| Gallery có ví dụ chép được | `docs/design-system/preview.html` |
| Prompt giao việc cho agent | `.ai/prompts/implement-page-with-team-design-system.md` |

**Mở gallery:** mở trực tiếp `docs/design-system/preview.html` bằng trình duyệt (cần
internet vì dùng đúng các CDN của ứng dụng). Gallery nạp CSS/JS thật của repo; thu/phóng
cửa sổ để xem 1440 / 768 / 390 và phóng 200%. Mục "Xem HTML" dưới mỗi mẫu là markup chép
được. Gallery tĩnh **không** chứng minh Razor, phân quyền hay tương tác của ứng dụng:
vẫn phải kiểm tra trên trang thật.

Khung một trang học viên:

```cshtml
@using TCTEnglish.ViewModels
@{
    ViewData["Title"] = "Tên trang";

    // Dựng ViewModel trong khối @{ } (đừng lồng dấu nháy kép trong thuộc tính model="...").
    var header = new PageHeaderViewModel
    {
        Title = "Tên trang",
        Description = "Một câu mô tả.",
        Breadcrumbs = new[]
        {
            new PageBreadcrumbItem("Từ vựng", Url.Action("Index", "Vocabulary")),
            new PageBreadcrumbItem("Tên trang")
        }
    };
    var empty = new StateMessageViewModel { Kind = StateMessageKind.Empty, Title = "Chưa có dữ liệu" };
}

@section Styles {
    <link rel="stylesheet" href="~/css/my-feature.css" asp-append-version="true" />
}

<div class="tct-theme tct-page my-feature-page">
    <partial name="_PageHeader" model="header" />

    <section class="tct-toolbar mb-3"> ... </section>

    @if (!Model.Items.Any())
    {
        <partial name="_StateMessage" model="empty" />
    }
</div>
```

CSS riêng của trang chỉ dùng token, phạm vi hẹp:

```css
/* wwwroot/css/my-feature.css */
.my-feature-page .lesson-tile {
    padding: var(--tct-space-md);
    border-radius: var(--tct-radius-lg);
    background: var(--tct-surface);
    color: var(--tct-text);
}
```

Partial chỉ nhận ViewModel có kiểu, mã hoá toàn bộ text và chỉ chấp nhận URL nội bộ.
Mỗi trang đúng **một** `<h1>` (partial `_PageHeader` đã tạo nó). Nút POST viết trong trang
để form và antiforgery token nằm cùng chỗ.

## 2. Quy trình làm một màn hình

1. **Đồng bộ:** sau khi chủ dự án cho phép pull, chạy
   `pwsh -NoProfile -File scripts/pull-local.ps1`. Pull code thành công chưa đủ: cần mã thoát 0
   và dữ liệu dùng chung đã đồng bộ (xem mục 8).
2. **Nhánh chủ đề:** `feature/<ten-man-hinh>` hoặc `fix/...`, không dùng tiền tố `codex/`.
3. **Xác định phạm vi:** route, vai trò, trạng thái (loading/empty/error/success), file
   view/CSS/JS bạn sở hữu. Việc nằm ngoài (ví dụ đổi layout chung) ghi thành đề xuất.
4. **Chọn component** từ gallery/DESIGN.md trước khi tự tạo. Thiếu component: làm theo mục 5.
5. **CSS có phạm vi:** `wwwroot/css/{feature}.css`, selector bắt đầu bằng class của trang,
   giá trị lấy từ `var(--tct-*)`. Không khai báo `:root` mới, không hex riêng, không selector phần tử toàn cục.
6. **Kiểm tra trên trang thật:** `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`
   (nếu cổng bận hãy dùng cổng khác, đừng tắt tiến trình của người khác). Dùng tài khoản test
   cục bộ trong `.appdata/local/access.json`. Xem 1440, 768, 375/390, phóng 200%, văn bản tiếng
   Việt/Anh dài, Tab/Escape, trạng thái lỗi.
7. **Review:** tự xem diff, điền checklist ở mục 7, rồi nhờ chủ dự án review. Commit, PR và merge
   cần chủ dự án cho phép ở từng bước.

## 3. Bắt buộc và linh hoạt

Bảng đầy đủ nằm trong DESIGN.md (mục *Mandatory versus page-level*). Tóm tắt: **bắt buộc**
là màu, font/độ đậm, shell và offset, hình dạng control, focus ring, validation, bốn trạng thái
UI, nền tảng truy cập (a11y), một `<h1>`. **Chủ trang quyết định** bố cục nội dung, thứ tự mục,
mật độ thông tin và component riêng được dựng từ token. Màn học (practice) và màn Admin không
buộc dùng cùng một bố cục.

## 4. Ranh giới trách nhiệm

| Thuộc nền tảng chung (cần review riêng khi sửa) | Thuộc chủ màn hình |
| --- | --- |
| `tct-learning-ui.css`, `layout.css`, `site.css` | `Views/<Feature>/**`, `wwwroot/css/<feature>.css`, `wwwroot/js/<feature>.js` |
| `_Layout.cshtml`, `_AdminLayout.cshtml`, `_PageHeader`, `_StateMessage` | ViewModel và controller của tính năng (theo quy tắc kiến trúc) |
| `DESIGN.md`, `AGENTS.md`, gallery | Dữ liệu mẫu và nội dung của tính năng |

Phân công người–màn hình do chủ dự án quyết định; bảng ở mục 6 để trống cột *Chủ sở hữu*
cho đến khi được gán. Không tự suy ra phân công.

## 5. Thay đổi CSS/layout chung hoặc thiếu token

- Sửa file ở cột trái của mục 4 là **thay đổi dùng chung**: mở PR riêng, mô tả mọi màn hình
  bị ảnh hưởng, đính kèm so sánh trước/sau (ảnh không chứa thông tin cá nhân) và chờ chủ dự án review.
- Thiếu token hay component: tạo đề xuất ngắn (đặt trong PR hoặc `docs/superpowers/plans/`) gồm
  *vấn đề*, *giải pháp*, *giá trị và tên đề xuất*, *các màn hình sẽ dùng*, *các consumer hiện có bị
  ảnh hưởng* (tìm bằng `grep` tên class/biến), *kiểm tra tương phản*. Chỉ dùng rộng sau khi được duyệt.
  Trong lúc chờ, dùng giải pháp cục bộ có phạm vi trong CSS của trang, đừng sao chép lệch chuẩn.
- Giá trị token dùng chung phải có kiểm tra tương phản (gallery tính sẵn). Không đổi giá trị token
  cũ đang có consumer (`--tct-white`, `--tct-text-main`, `--tct-border-input`, `--tct-font-family`)
  mà không kiểm tra `auth-stitch.css` và `listening-index.css`.

## 6. Legacy screens and adoption tracker

Màn hình cũ chưa dùng token. Khi chuyển một màn hình: thay hex/biến riêng bằng `var(--tct-*)`,
bọc `tct-theme`, dùng partial/component chung, xoá `:root` riêng của trang, cập nhật dòng ở đây
trong cùng PR. Tránh định nghĩa cạnh tranh: **không** thêm biến mới có cùng ý nghĩa với token.

| Màn hình | Route / view | CSS hiện tại | Hệ token riêng đang dùng | Trạng thái | Chủ sở hữu |
| --- | --- | --- | --- | --- | --- |
| Dashboard | `/Home/Index` | `Dashboard.css` | `--brand`, `--surface`, `--radius` (không tiền tố) | Chưa chuyển | _chủ dự án gán_ |
| Từ vựng | `/Vocabulary/*` | `vocabulary-system.css` (4,7k dòng) | `--primary`, `--gray-*` (không tiền tố) | Chưa chuyển | _chủ dự án gán_ |
| Nói | `/Speaking`, `/Speaking/Practice` | `speaking.css` | `--stitch-*`, `--spk-*` | Chưa chuyển | _chủ dự án gán_ |
| Nghe | `/Home/Listening`, `/Home/Listening/Practice/{id}` | `listening-index.css`, `listening-practice.css` | `--li-*`, `--lp-*` | Chưa chuyển | _chủ dự án gán_ |
| Viết | `/Home/Writing*` | `writing-index.css`, `writing-exercises.css`, `writing.css` | `--wi-*`, `--we-*`, `--w-*` | Chưa chuyển | _chủ dự án gán_ |
| Đọc | `/Home/Reading*` | `Reading.css`, `reading-study.css` | `--rd-*` | Chưa chuyển | _chủ dự án gán_ |
| Mục tiêu | `/Goals` | `goals.css` | `--md-sys-color-*`, `--stitch-*` | Chưa chuyển | _chủ dự án gán_ |
| Tài khoản | `/Account/Profile`, `/Account/Settings` | `account.css` | Material không tiền tố (cũng nạp vào Admin) | Chưa chuyển | _chủ dự án gán_ |
| Đăng nhập / quên mật khẩu | `/Account/Login` … (`Layout = null`) | `auth-stitch.css` + `tct-learning-ui.css` | Tên `--tct-*` cũ | Dùng tên cũ | _chủ dự án gán_ |
| Lớp học, thư mục | `/Class/*`, `/Folder/*` | `Class-Detail.css` | Giá trị cố định | Chưa chuyển | _chủ dự án gán_ |
| Thông báo | `/Notification` | `notification-page.css`, `notifications.css` | Giá trị cố định | Chưa chuyển | _chủ dự án gán_ |
| AI chat | `/Ai/Chat` | `ai-chat.css`, `ai-chat-launcher.css` | Giá trị cố định | Chưa chuyển | _chủ dự án gán_ |
| Liên hệ | `/Home/Contact` | `contact.css` | Giá trị cố định | Chưa chuyển | _chủ dự án gán_ |
| Bộ thẻ cá nhân | `/Sets`, `/Sets/{id}`, `/Sets/{id}/Add`, `/Sets/{id}/Import`, `/Sets/Cards/{id}`, `/s/{slug}` | `personal-sets.css` | `--tct-*` | Đã dùng token | Huỳnh Phú Trọng |
| Luyện tập | `/Practice`, `/Practice/{mode}`, `/Practice/HardWords` | `practice.css` | `--tct-*` | Đã dùng token | Huỳnh Phú Trọng |
| Admin (toàn bộ) | `/Admin/*` | Bootstrap + `account.css` + `<style>` trong layout | `--sidebar-width` (đã nối vào token). Lưu ý: `account.css` ép `.text-secondary { color: #745b00 !important }` trên mọi trang Admin | Chưa chuyển | _chủ dự án gán_ |

Các vấn đề cũ đang chờ chủ dự án quyết định (tương phản, chiều cao header, `#folderModal`,
font gốc 14px trên mobile…) nằm ở mục *Open proposals* của DESIGN.md.

## 7. Checklist PR cho màn hình giao diện

- [ ] Dùng `tct-theme`, token và component chung; không hex/font/bo góc riêng, không `:root` mới, không `!important` tràn lan.
- [ ] Responsive: 1440, 768, 375/390 và 200% zoom, không tràn ngang (kiểm tra cả phần tử bị `overflow-x: hidden` che).
- [ ] Bàn phím: Tab theo đúng thứ tự, focus nhìn thấy rõ, Escape đóng hộp thoại/ngăn kéo và trả focus.
- [ ] Tương phản: chữ 4.5:1 (chữ lớn 3:1), viền control và icon quan trọng 3:1; không dùng màu làm tín hiệu duy nhất.
- [ ] Văn bản dài (tiếng Việt có dấu và tiếng Anh, từ dài không dấu cách) không bị cắt mất nội dung học.
- [ ] Đủ trạng thái: loading, empty, error, success, disabled, pending (chống gửi trùng).
- [ ] Giữ nguyên hành vi: route, HTTP/JSON/SignalR, selector/ID/`data-*` mà JS đang dùng, antiforgery, validation, kiểm tra quyền.
- [ ] Ảnh chụp dùng dữ liệu tổng hợp, không chứa email/token/thông tin cá nhân; lưu trong `.tmp/<tên>/` (đã bị git bỏ qua), không commit.
- [ ] Đã cập nhật dòng của màn hình ở mục 6 (nếu chuyển một màn hình cũ).
- [ ] `dotnet build` và kiểm tra liên quan đã chạy; ghi rõ phần nào **chưa** kiểm tra.

## 8. Dữ liệu, Docker và khoá

Mỗi máy giữ Docker MariaDB và khoá API riêng; không sao chép volume, backup hay khoá của người khác.
Thay đổi **chỉ giao diện** không cần gói dữ liệu rỗng. Nếu bạn thật sự thay đổi nội dung dùng
chung (bài học, từ vựng…), làm theo [shared-data-workflow](../operations/shared-data-workflow.md)
(`scripts/shared-data.ps1`): xuất gói đã chọn, review gói trong cùng PR. Không dùng bản dump
database để đồng bộ team.

Quy trình đưa ảnh/thay đổi lên shared PR, commit, merge và deploy vẫn do chủ dự án cho phép từng bước.
