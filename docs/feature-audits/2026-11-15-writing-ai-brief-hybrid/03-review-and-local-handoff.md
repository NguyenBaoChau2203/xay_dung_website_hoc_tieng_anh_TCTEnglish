# Review tích hợp Writing và bàn giao local — 15/11/2026

Review theo yêu cầu chủ repo: kiểm phần Gemini đã làm, sửa trực tiếp các lỗi còn
lại và chạy local. Một agent thực hiện toàn bộ. Chỉ web TCT ở cổng5100 theo chỉ
dẫn mới nhất; MariaDB vẫn chạy để phục vụ ứng dụng. Không commit/push/deploy.

Biên bản này thay thế các tuyên bố hoàn tất hoặc phạm vi kiểm chưa chính xác
trong biên bản02 ban đầu. Hợp đồng backend hiện tại nằm ở backend-contract.md.

## Phát hiện và sửa

| Mức | Vấn đề thực tế trước review | Kết quả sửa |
|---|---|---|
| P1 | sessionStorage chỉ được xóa, không lưu receipt; refresh mất khóa duy nhất khi response đầu chưa tới | Lưu key, SHA256 fingerprint và operation ID theo owner; không lưu brief/source/slot PII. Có API đọc receipt theo key khi chưa biết operation ID |
| P1 | Mode/input thay đổi được trong dispatch; pageshow mở lại submit; confirm/cancel không có fencing callback đầy đủ | Khóa control khi đang xử lý/chưa rõ kết quả, sequence chống callback cũ; phục hồi bằng GET trước, không tự POST khi quay lại trang |
| P1 | Cancel nuốt lỗi rồi luôn báo đã hủy, làm mất trạng thái | Chỉ báo đã hủy từ trạng thái server; mất acknowledgement giữ receipt, yêu cầu kiểm tra. Bài đã commit vẫn được phục hồi |
| P1 | Chỉ xóa storage tài khoản khác khi load; không che form/private cards hoặc kiểm owner sau blur/focus | Che vùng cá nhân trước snapshot và kiểm owner thực qua quota endpoint khi activate; mismatch reload, loại bỏ dữ liệu tài khoản cũ |
| P2 | Hạn mức tĩnh “5 bài AI” kể cả Admin và không phản ánh số thành công/reservation | API quota từ log chung/lease thật, Admin unlimited, số lượt còn lại và reset UTC00:00/07:00 UTC+7 |
| P2 | Preview lặp tiếng Việt, thiếu tiếng Anh; label slot không liên kết input; chỉ chọn frame đầu | Preview hai ngôn ngữ đúng một lần, label/ID đầy đủ; hỗ trợ chọn mọi candidate server cung cấp, đổi khung phải xác nhận lại |
| P2 | Quota chip nowrap và grid min-content làm form rộng576px ở390px | minmax/min-width, chip xuống dòng, tab co đúng; doc width bằng viewport ở1440/1366/768/390 |
| P2 | Lời giải thích coi khung là đáp ứng toàn mô tả và ngụ ý cả lần tạo không phát sinh usage | Nêu rõ chi tiết ngoài khung bị bỏ với chấp nhận của người học; khung không gọi thêm OpenAI, lần trước có thể có usage |
| P2 | CSS mới có selector chưa scope, giá trị màu riêng; mô tả đầu trang vẫn chỉ nói dán nguồn | Scope trong writing-exercises-page, dùng token, cập nhật hai mode và ví dụ phù hợp catalog hiện tại |

Chỉ mở rộng service/controller/ViewModels hiện có. Hai API đọc mới là
`GET /api/writing/generation/by-key/{key}` và `GET /api/writing/generation/quota`.
Owner lấy từ server; foreign receipt404, response no-store, không gọi provider.
Không có thay đổi persisted model/mapping mới, nên không có migration mới của
đợt review. Migration step01 còn thiếu trên dev đã được launcher chuẩn áp dụng
sau backup ignored; DB không bị reset hay thay bằng bản sao.

## Regression và build

-4 Node regression mới chạy đỏ trước sửa: mất receipt, đổi mode trong dispatch,
  cancel thất bại báo thành công và thiếu tiếng Anh trong preview.
- Node cuối: **72 pass /0 fail /0 skip**, gồm Writing brief/UI/recent/draft/
  assessment và Goals. Có reload/by-key không POST, HTML error giữ receipt,
  account mismatch, confirm double-submit, chọn frame và ma trận refusal/quota/
  configuration/cancel không tạo fallback giả.
- Full Release: **1.630 pass /0 fail /53 skip**, tổng1.683,5m38s,
  `.tmp/writing-brief-review/review-full.trx`. Paid/live flags tắt trong suite;
  skip không được tính là pass.
- Focused API/Razor/recent: **26 pass /0 fail**. Hai test API mới kiểm key bị mất,
  access, no-store, quota success/reservation/expiry, Admin và snapshot learning.
- Sau các sửa markup cuối, recheck render/API: **11 pass /0 fail**,
  `review-final-render.trx`. Application build được thực thi qua test và launcher;
  warning infrastructure/test và các warning có sẵn không bị nới để làm xanh.
- `git diff --check` sạch sau phục hồi11 email preview vốn clean chỉ bị full suite
  ghi lại line-ending. Không phục hồi bất kỳ owner edit nào.

## Browser đã quan sát

Chrome DevTools MCP, context riêng với2 tài khoản local tổng hợp mới. Cookie login,
antiforgery, controller và MariaDB là thật. Có hai loại injection được ghi riêng:
response interception để kiểm trạng thái UI và endpoint OpenAI loopback đóng cổng9
với key tổng hợp ở child environment để kiểm outage qua transport/backend thật.

1. Native ArrowRight/ArrowLeft đổi tab và focus; dữ liệu hai tab độc lập. Ví dụ
   chỉ điền form, không tự submit. Header/counter/control đúng contract1200,
   short6–8/medium10–12/long14–16.
2. Offer hiển thị đủ Việt/Anh, các label slot có input đích, confirm bị chặn
   trước khi thông tin/checkbox hợp lệ. Màn390px đã xem ảnh trực tiếp; sau sửa
   cả1440/1366/768/390 đều có0 phần tử form tràn viewport.
3. Confirm-template thật đã commit bài135, sau đó giả lập mất response. UI giữ
   receipt; kiểm tra GET trả đúng bài, đúng1 confirm POST, origin nội bộ và quota
   thành công1. Không tạo lại bằng khóa mới.
4. Mở practice thật hiển thị origin nội bộ; native Back có recent của bài vừa mở
   và không có POST generation tự động. Tạo/xem preview không tự ghi recent.
5. Event blur/focus được phát có kiểm soát trên DOM thật: che vùng cá nhân,
   đổi cookie sang tài khoản mới rồi focus dẫn tới reload. Đây không phải bằng
   chứng native tab-focus hoặc một restored bfcache thực sự.
   Tài khoản mới không thấy draft, private card hoặc recent của tài khoản trước.
6. Sau khi bật boundary process chỉ loopback/key giả: source-mode outage giữ
   nguyên văn bản, không offer template, payload chỉ mode/sourceText/key.
7. Mất response đầu được inject sau khi backend đã lưu receipt. Storage lúc đó
   có key nhưng chưa có operation ID. Refresh phục hồi offer thật theo key;
   **0 generation POST** sau reload, không tiêu thụ thêm lượt.
8. Reduced-motion được emulate và đọc lại bằng matchMedia. Native200% zoom chưa
   xác minh: Control++ qua MCP không đổi DPR/viewport/visual scale. Không dùng CSS
   zoom để thay thế hoặc gọi đó là native zoom.

Ảnh được xem trực tiếp qua screenshot MCP; tùy chọn lưu file của MCP gặp lỗi
workspace-path trên Windows nên không đưa đường dẫn ảnh giả vào biên bản. Dữ liệu
đo và receipt riêng ở `.tmp/writing-brief-review/browser-evidence.json`.

## Sự cố gọi live ngoài ý muốn

Yêu cầu không chạy test trả phí đã bị vi phạm một lần trong browser review.
Lớp chặn fetch không tồn tại sau reload do đổi tài khoản; một POST from_text đã
tới provider thật trước khi nhận ra guard đã mất. Receipt ghi **741 token đã
biết**, succeeded, UserId74 là tài khoản tổng hợp của đợt review. Chưa xác minh
số tiền, không coi đây là paid acceptance được chủ repo cho phép và không dùng
lượt này để tuyên bố chất lượng model live đạt yêu cầu.

Agent đã báo ngay trong chat, dừng web đó và đổi phần kiểm tiếp theo sang key
tổng hợp + `http://127.0.0.1:9/v1` chỉ ở child environment. Cấu hình/key được lưu
của chủ repo không bị ghi lại. Đây là boundary chính sau sự cố, không còn phụ
thuộc lớp chặn browser. Metadata sự cố ở file ignored
`.tmp/writing-brief-review/live-attempt-incident.json`. Không có giá/token hay số
tiền được suy đoán. Các lượt loopback sau đó không thể tới OpenAI thật; usage upper
bound bảo thủ của transport không phải bằng chứng phát sinh billing.

Trước bàn giao đã đóng context kiểm thử và dừng launcher loopback. Launcher cuối
dùng cấu hình local bình thường để chủ repo có thể sử dụng; không thao tác tạo
bài/ASR thêm trong phiên bàn giao đó. Không được ghi “không có paid call” cho toàn
bộ đợt review này.

## Local và bảo toàn

Các web TCT cũ đã dừng theo chỉ dẫn mới; bản bàn giao dùng
`pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5100`.
MariaDB local và các dịch vụ/dự án khác được giữ. Backup trước startup nằm trong
ignored `.tmp/writing-brief-review/before-start.private.sql`.

Hai tài khoản tổng hợp và fixture riêng mới chỉ phục vụ review, không thay role,
password hoặc learner data của tài khoản hiện hữu. Không sửa các package108 bài,
catalog, production manifest, migration cũ, protected config, User Secrets hay
trust settings. Không có commit/push/CI/staging/production. Kết quả cuối về
readiness/port/speech và preservation được ghi ở phần bàn giao cuối bên dưới.

Kết quả bàn giao cuối: web PID20884 lắng nghe5100 (IPv4/IPv6), không còn web TCT
ở5115/5116/5117. `/health/ready`200. Launcher cuối báo key từ **User Secrets**,
không dùng key giả/endpoint loopback của phiên verification. Speech status:
configured, `gpt-transcribe`, `VerifiedLive:false`; không thực hiện ASR live.
Shared-data startup kiểm379 releases, **0 record operations**. EF9.0.4 repo-local
báo không pending model changes. Node cuối72 pass; render/API cuối11 pass.

Receipt preservation `.tmp/writing-brief-review/final-preservation.json` xác nhận
109 file shared-content trong baseline và toàn bộ migration/Designer/snapshot giữ
nguyên hash so đầu review; production manifest giống HEAD. Các thay đổi ban đầu
Writing/Goals/recent khác vẫn được giữ. Hai tài khoản UAT mới giữ riêng ở local;
credential, backup SQL và fixture receipt đều ignored. Cấu hình bảo vệ không đổi.

## Thứ tự sử dụng/kiểm thủ công

1. Mở `http://localhost:5100/Home/Writing/Exercises`, đăng nhập tài khoản hiện có.
2. Premium/Admin: chọn mode, dùng ví dụ rồi chỉnh mô tả/controls; kiểm hạn mức
   chung. Standard/guest giữ giới hạn quyền như trước.
3. Nếu OpenAI chưa tạo được bài và có khung tương thích: đọc cả hai ngôn ngữ,
   chọn khung, điền slot và xác nhận giới hạn. Chủ đề/length/tone ngoài catalog
   không được tự thay bằng một bài khác.
4. Nếu mất response: dùng “Kiểm tra yêu cầu trước” hoặc tải lại; không mở một
   yêu cầu mới để né trạng thái chưa rõ. Bài hoàn tất có link mở lại.
5. Vào practice rồi Back; thử đổi tài khoản để kiểm dữ liệu riêng. Native200%
   zoom còn cần quan sát bằng trình duyệt người dùng thật.
