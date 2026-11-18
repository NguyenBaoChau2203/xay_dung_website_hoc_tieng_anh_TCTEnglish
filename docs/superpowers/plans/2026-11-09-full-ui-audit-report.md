# Full UI/UX Audit Report — TCT English

**Date:** 2026-11-09  
**Mode:** Audit + report + repair plan only (no product code changes)  
**Environment:** Local `http://127.0.0.1:5100` (`/health/ready` = 200)  
**Role used:** Local Admin (authenticated learner + Admin); Auth pages in isolated anonymous browser context  
**Baselines:** `DESIGN.md`, `docs/superpowers/specs/2026-11-09-multi-platform-ui-design.md`, `docs/design-system/team-ui-workflow.md`  
**Evidence notes:** `.tmp/ui-audit/2026-11-09/` (inventory + metrics; screenshots captured in DevTools session)

---

## 1. Tổng quan Audit

| Nội dung | Kết quả |
| --- | --- |
| Tổng số trang/route trong inventory | 28 (2 Auth + 15 learner + 11 Admin) |
| Tổng số trang đã kiểm tra (ít nhất 1 viewport) | 26 |
| Viewport đã chạy | 1920×1080, 1366×768, 768×1024, 390×844 |
| Tổng số lỗi Confirmed | 14 |
| Critical | 0 |
| Major | 8 |
| Minor | 5 |
| Cosmetic | 1 |
| Suspected | 2 |
| Số khu vực Not Tested | 7 nhóm (xem §1.1) |

### 1.1. Not Tested (có lý do)

| Khu vực | Lý do |
| --- | --- |
| Speaking Practice deep | Không mở practice item trong đợt này |
| Reading Study / Writing editor deep | Không mở bài chi tiết |
| Class Detail / Folder Detail | Không mở detail (Folder index empty) |
| Vocabulary set/card detail | Chỉ kiểm tra `/Vocabulary/Index` |
| AI Chat send message | Cấm gửi AI/paid trong audit |
| Contact / CreateClass POST thành công | Cấm mutate; chỉ quan sát DOM/HTML5 |
| Zoom 200% toàn site | Chưa đo systematic (shell/menu đã đạt 44px) |

### 1.2. Cấu trúc đã xác nhận (Bước 1)

- Stack: ASP.NET Core MVC/Razor net9, Bootstrap 5.3.2, jQuery/vanilla JS, MariaDB local.
- Learner shell: `_Layout.cshtml`, `layout.css`, `layout.js`, `tct-learning-ui.css`.
- Admin shell: `_AdminLayout.cshtml` + `account.css`.
- Hầu hết feature CSS vẫn legacy (tracker §6: Chưa chuyển token).

---

## 2. Danh sách lỗi giao diện

| ID | Trang/Khu vực | Thành phần | Mô tả lỗi | Severity | Trạng thái | Bằng chứng |
| --- | --- | --- | --- | --- | --- | --- |
| UI-001 | Nhiều trang learner (Dashboard, Vocabulary, Listening, Reading, Writing, Speaking, Goals, Class, Folder, AI Chat, Contact, …) | Page root | Không có `.tct-theme` → Bootstrap không map sang token TCT theo DESIGN | Major | Confirmed | DevTools `document.querySelector('.tct-theme') === null` trên các route đã đo |
| UI-002 | Listening index, Speaking, Auth, Account; một số trang body | Typography | Font không thống nhất với chuẩn Inter: Listening/Speaking dùng Be Vietnam Pro (`--tct-font-family` legacy); Dashboard/Writing/Goals đo `system-ui` trên `body` | Major | Confirmed | Computed `font-family` tại 1920; `listening-index.css` dòng font-family |
| UI-003 | Learner shell (mọi trang `_Layout`) | Header | Chiều cao header thực tế ~82px trong khi token `--tct-header-h` = 72px → offset sidebar/main lệch vài px (đã ghi trong DESIGN Open proposals) | Major | Confirmed | 1920/1366: `headerH=82`, token `72px` |
| UI-004 | Learner footer (mọi trang `_Layout`) | Footer brand | Copy/brand footer vẫn “TCT Vocabulary”, email/domain `tctvocab*` trong khi sản phẩm là TCT English | Major | Confirmed | Snapshot CreateClass; `_Layout.cshtml` footer markup |
| UI-005 | `/Home/Folder` | Page heading | Không có `<h1>`; tiêu đề chính là `<h2>Your folders</h2>` | Major | Confirmed | 1920 & 390: `h1Count=0`; `Views/Folder/Folder.cshtml` |
| UI-006 | `/Home/Reading`, `/Home/Class` | Document title | `document.title` = `- TCTEnglish` (thiếu ViewData Title) | Minor | Confirmed | DevTools title; `Reading.cshtml` / `Class.cshtml` không set `ViewData["Title"]` |
| UI-007 | Admin area (Dashboard, Users, Billing, …) | `.text-secondary` | Màu chữ secondary bị ép `#745b00` (rgb(116,91,0)) do `account.css` `!important` — lệch Bootstrap/TCT muted | Major | Confirmed | Computed color trên Admin; `account.css` ~L960; đã nêu trong team-ui-workflow |
| UI-008 | `/Admin/Dashboard` | Heading | Không có `<h1>`; dùng `<h2>Dashboard</h2>` | Major | Confirmed | 1920/390 `h1Count=0`; `Areas/Admin/Views/Dashboard/Index.cshtml` |
| UI-009 | `/Admin/ReadingManagement` | Document title | Title `— TCT Admin` (thiếu ViewData Title) | Minor | Confirmed | DevTools title; view Index không set Title |
| UI-010 | `/Account/Login` (390) | Nút đóng | Touch target close ~40×40 < 44px (`--tct-target-min`) | Minor | Confirmed | Anon context 390: closeBtn w/h=40 |
| UI-011 | Learner footer | Heading levels | Cột “Học tập” dùng `<h5>` trong khi “Khám phá”/“Kết nối” dùng `<h4>` | Minor | Confirmed | Accessibility snapshot CreateClass |
| UI-012 | Learner footer | Social links | Facebook/YouTube/GitHub `href="#"` (không điều hướng thật) | Minor | Confirmed | Snapshot links trên CreateClass |
| UI-013 | `/Home/Folder` | Copy ngôn ngữ | Tiêu đề English “Your folders” / “Saved folders” lẫn empty state tiếng Việt | Cosmetic | Confirmed | Screenshot 390 + DOM headings |
| UI-014 | Admin Billing Health | Tables | Một số bảng provider không bọc `.table-responsive` (3/4 false tại 1920) | Minor | Confirmed | DevTools `tableWrap:[false,false,false,true]` |
| UI-015 | Listening Practice `/Home/Listening/Practice/1` | Player | Không có `<audio>`; dùng `.lp-yt-wrapper` (YouTube). iframe/player có thể không hiển thị đủ trong session audit | Suspected | Suspected | DOM: `yt:true`, `audio:false`; screenshot 390 không thấy player rõ |
| UI-016 | `/Home/Contact` | Form | Script chỉ thấy `required` gắn `folderName` (modal tạo folder), chưa xác nhận đủ validation field contact | Suspected | Suspected | Form probe trên Contact; cần kiểm tra thêm markup contact |

### Quan sát dương (không phải lỗi)

- Learner footer/main offset khớp sidebar mở/thu tại 1920 & 1366 (`280px` / `0`).
- Mobile 390: drawer `inert`/aria đúng; menu toggle 44×44; không page overflow trên các trang đã đo.
- Admin 390: sidebar off-canvas; toggle 44×44; bảng Dashboard cuộn trong `.table-responsive`.
- Console error: không thấy trên Dashboard, AI Chat, Listening Practice (mẫu).
- Network CSS/font CDN 200 trên AI Chat (mẫu).

---

## 3. Đề xuất phương án khắc phục

| ID lỗi | Nguyên nhân | Phương án xử lý | File/component dự kiến | Ưu tiên |
| --- | --- | --- | --- | --- |
| UI-001 | Feature views chưa bọc `tct-theme` | Bọc root page `div.tct-theme` khi chuyển từng feature; ưu tiên shell-adjacent pages | Từng `Views/**/*.cshtml` theo tracker | P1 (Phase 3) |
| UI-002 | Legacy `--tct-font-family` / thiếu Inter trên body page | Chuẩn hóa font về Inter theo DESIGN; giới hạn Be Vietnam Pro đúng phạm vi Auth/Account nếu owner giữ legacy | `listening-index.css`, `speaking.css`, `auth-stitch.css`, `account.css`, feature CSS | P1 (Phase 3) |
| UI-003 | Token header thấp hơn header thực | Cập nhật `--tct-header-h` sau đo lại mọi breakpoint **hoặc** giảm padding header — cần quyết định Chau/owner | `tct-learning-ui.css`, `layout.css` | P1 (Phase 2) |
| UI-004 | Footer copy cũ | Đổi brand/contact/copyright sang TCT English (copy owner duyệt) | `_Layout.cshtml` | P0 (Phase 2) |
| UI-005 | Markup dùng h2 | Đổi heading chính thành `<h1>`; giữ visual qua CSS | `Views/Folder/Folder.cshtml` | P0 (Phase 1) |
| UI-006 | Thiếu `ViewData["Title"]` | Set Title trong view/controller | `Views/Study/Reading.cshtml`, `Views/Class/Class.cshtml` | P2 (Phase 5) |
| UI-007 | `account.css` override toàn cục `.text-secondary` | Scope selector chỉ Account/Stitch, không áp Admin; hoặc bỏ `!important` | `wwwroot/css/account.css`, `_AdminLayout` consumers | P0 (Phase 1) |
| UI-008 | Admin dashboard dùng h2 | Thêm/đổi thành một `<h1>` | `Areas/Admin/Views/Dashboard/Index.cshtml` | P1 (Phase 2) |
| UI-009 | Thiếu Title | Set `ViewData["Title"]` | `Areas/Admin/Views/ReadingManagement/Index.cshtml` | P2 (Phase 5) |
| UI-010 | Padding nút đóng nhỏ | `min-width/height: var(--tct-target-min)` | `auth-stitch.css` (`.auth-close-btn`) | P2 (Phase 4) |
| UI-011 | Markup footer không đồng cấp | Đổi h5 → h4 (hoặc ngược lại thống nhất) | `_Layout.cshtml` | P2 (Phase 5) |
| UI-012 | Placeholder `#` | Gắn URL thật hoặc `aria-disabled`/ẩn đến khi có | `_Layout.cshtml` | P2 (Phase 5) |
| UI-013 | Copy song ngữ lẫn | Thống nhất VI (hoặc i18n có chủ đích) | `Views/Folder/Folder.cshtml` | P2 (Phase 5) |
| UI-014 | Thiếu wrapper responsive | Bọc `.table-responsive` các bảng còn lại | `Areas/Admin/Views/BillingManagement/Health.cshtml` | P2 (Phase 4) |
| UI-015 | Cần xác minh player | Kiểm tra iframe YT load, fallback audio, trạng thái empty media | `Views/Study/ListeningPractice.cshtml`, `listening-practice.css` | P1 verify trước fix |
| UI-016 | Cần xác minh form Contact | Đọc markup Contact + test HTML5/unobtrusive validation (không POST) | Contact view + `contact.css` | P2 verify |

---

## 4. Đánh giá tính nhất quán UI/UX

**Hệ thống (systemic):**

1. **Đa hệ token CSS** — `--brand` / `--stitch-*` / `--spk-*` / `--li-*` / Material / hex cứng song song với `--tct-*` (tracker §6). Đây là nợ thiết kế lớn nhất.
2. **Font** — Inter (chuẩn) vs Be Vietnam Pro (Auth/Account + Listening/Speaking) vs `system-ui` đo được trên một số page body.
3. **Thiếu `tct-theme`** — Bootstrap primary/default không đồng bộ với `#4255ff` trên nhiều màn.
4. **Brand footer vs product name** — “TCT Vocabulary” trên mọi trang learner.
5. **Admin bị nhiễm `account.css`** — `.text-secondary` nâu vàng trên Admin.

**Cục bộ (one-off):**

- Folder thiếu h1 + copy English.
- Reading/Class/Admin ReadingManagement thiếu document title.
- Auth close target 40px.
- Footer heading level / social `#`.

**Responsive:**

- Shell learner/Admin drawer và offset footer ở các viewport đã đo **ổn**.
- Chưa thấy Critical overflow trang trên mẫu đã quét.
- Practice/deep screens và zoom 200% chưa phủ đủ để tuyên bố “Đạt đa nền” toàn site.

**UX journey (tóm tắt):**

- Dashboard/Listening/Speaking: mục đích trang rõ (có h1).
- Folder: empty state rõ CTA, nhưng heading/language yếu.
- Admin Dashboard: nhiệm vụ OK nhưng hierarchy heading + màu secondary gây lệch chuẩn.
- Auth Login: tách brand/form rõ; mobile stack OK.

---

## 5. Kế hoạch chỉnh sửa chi tiết

**Không triển khai cho đến khi owner phê duyệt từng Phase.**

### Phase 1 – Critical Fixes

Không có Critical Confirmed trong đợt này.  
**Phase 1 thực tế = blocker a11y/Admin spoiling:**

| Việc | Lỗi | Mục tiêu | File | Thay đổi | Ảnh hưởng | Nghiệm thu |
| --- | --- | --- | --- | --- | --- | --- |
| P1-A | UI-007 | Admin không còn `.text-secondary` nâu | `account.css` | Scope/remove override | Mọi trang Admin + Account | Computed color ≠ `#745b00` trên Admin; Account vẫn đúng design Account |
| P1-B | UI-005 | Folder có đúng 1 h1 | `Folder.cshtml` | h2→h1 (style giữ) | Folder index | h1Count=1; 390/1920 không regression |

### Phase 2 – Major UI Fixes

| Việc | Lỗi | Mục tiêu | File | Thay đổi | Ảnh hưởng | Nghiệm thu |
| --- | --- | --- | --- | --- | --- | --- |
| P2-A | UI-004 | Footer đúng brand TCT English | `_Layout.cshtml` | Copy/contact/copyright | Mọi trang learner | Text footer khớp naming owner duyệt |
| P2-B | UI-008 | Admin Dashboard 1 h1 | Admin Dashboard view | Heading | Admin home | h1 present |
| P2-C | UI-003 | Header offset khớp layout | tokens + `layout.css` | Cập nhật token **sau** đo lại 1920/1366/768/390 | Shell toàn learner | Không che content; sidebar top khớp |
| P2-D | UI-015 | Xác minh + sửa player Listening Practice nếu thiếu | ListeningPractice + CSS | Fallback/empty state nếu YT fail | Practice | Audio/YT controls visible hoặc empty rõ |

Phụ thuộc: P2-A độc lập; P2-C nên sau khi đo lại (shared-impact).

### Phase 3 – UI Consistency

| Việc | Lỗi | Mục tiêu | File | Thay đổi | Ảnh hưởng | Nghiệm thu |
| --- | --- | --- | --- | --- | --- | --- |
| P3-A | UI-001 | Bọc `tct-theme` theo từng feature | Feature views | Class wrapper | Từng feature | Buttons/inputs theo token |
| P3-B | UI-002 | Font Inter theo DESIGN | Feature CSS | Bỏ cascade Be Vietnam ngoài Auth/Account nếu policy giữ | Listening/Speaking/… | Computed font Inter trên learner non-auth |

Phụ thuộc: làm theo tracker §6 từng feature (whole-feature multi-platform).

### Phase 4 – Responsive Optimization

| Việc | Lỗi | Mục tiêu | File | Thay đổi | Ảnh hưởng | Nghiệm thu |
| --- | --- | --- | --- | --- | --- | --- |
| P4-A | UI-010 | Auth close ≥44px | `auth-stitch.css` | min touch | Login mobile | 390 ≥44 |
| P4-B | UI-014 | Health tables responsive | Health.cshtml | `.table-responsive` | Billing Health | 390 không tràn page |
| P4-C | — | Deep practice audit còn thiếu | Practice views | Audit rồi fix | Study skills | Cập nhật tracker Đa nền |

### Phase 5 – Minor & Cosmetic

| Việc | Lỗi | Mục tiêu | File |
| --- | --- | --- | --- |
| P5-A | UI-006, UI-009 | Document titles | Reading/Class/Admin ReadingManagement |
| P5-B | UI-011, UI-012 | Footer headings + social | `_Layout.cshtml` |
| P5-C | UI-013 | Copy Folder VI/EN | `Folder.cshtml` |
| P5-D | UI-016 | Xác minh Contact validation | Contact views |

---

## 6. Checklist nghiệm thu sau chỉnh sửa

- [ ] Lỗi Critical đã được xử lý (nếu phát sinh thêm ở vòng sau).
- [ ] Lỗi Major Phase đã duyệt đã được xử lý (UI-007, UI-005, UI-004, UI-008, UI-003, … theo Phase).
- [ ] Các component đồng nhất hơn trong phạm vi feature đã sửa (`tct-theme` nơi đã adopt).
- [ ] Typography đồng nhất với DESIGN trên surface đã sửa.
- [ ] Màu sắc đúng hệ thống thiết kế (Admin không còn `#745b00` trên `.text-secondary`).
- [ ] Không còn lỗi tràn layout trên 1920 / 1366 / 768 / 390 cho surface đã sửa.
- [ ] Responsive shell (sidebar/footer) vẫn đúng.
- [ ] Các tương tác giao diện an toàn (toggle menu, dropdown) hoạt động.
- [ ] Không phát sinh Console Error mới trên trang đã sửa.
- [ ] Không làm ảnh hưởng route/JSON/SignalR/antiforgery/access hiện có.

---

## 7. Stop gate

**Đợt audit kết thúc tại đây.**  
Không chỉnh sửa mã sản phẩm, không commit, không deploy trong đợt này.

Chờ chỉ thị rõ ràng, ví dụ:

`TÔI ĐỒNG Ý PHÊ DUYỆT PHASE 1 – BẮT ĐẦU TRIỂN KHAI`

Nếu chỉ duyệt một Phase, không triển khai Phase khác.
