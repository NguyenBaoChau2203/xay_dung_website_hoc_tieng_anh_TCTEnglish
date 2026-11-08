# Phase 6 — báo cáo trình duyệt cục bộ

> Báo cáo lịch sử của lần nộp BLOCKED. Xem [browser closeout](browser-closeout.md)
> cho bằng chứng bổ sung và [handoff hiện tại](local-completion-handoff.md).
> Không dùng các hàng chưa chạy dưới đây làm trạng thái hiện tại.

Ngày chạy: 2026-11-07. Đây là nghiệm thu giao diện với provider giả. Không phải đánh giá chất lượng model.

## Môi trường

- Web: `http://localhost:5186`, cấu hình Release, khởi động bằng `scripts/start-local.ps1 -NoBrowser`. `/health/ready` trả 200 `{"status":"ready"}` lúc bàn giao.
- Phiên trình duyệt Cursor đã đăng nhập sẵn bằng admin cục bộ. Không nhập lại mật khẩu.
- Provider của tiến trình web: BaseUrl loopback `http://127.0.0.1:18086`, model `gpt-6-luna`, reasoning `low`. Khóa trong biến môi trường con là khóa giả, không phải khóa đã lưu.
- `scripts/speech-status.ps1` đọc kho User Secrets đã lưu: configured, model `gpt-transcribe`, VerifiedLive false. Lệnh này không gửi audio. Nó không mô tả BaseUrl của tiến trình web.
- `loopback.cjs` lắng nghe `127.0.0.1:18086` và chỉ tạo `.appdata/local/phase6-browser/requests.jsonl` khi có request. File đó không xuất hiện sau các hành trình. Lúc bàn giao không còn tiến trình listen cổng 18086.
- MariaDB Docker local giữ nguyên. Không reset.

Ảnh, không chứa mật khẩu, nằm trong `.appdata/local/phase6-browser/`:

- `phase6-full-reload-sources.png`
- `phase6-embed-long-title.png`
- `phase6-narrow-390.png`
- `phase6-zoom-200.png`

## Hành trình đã làm

Trang đầy đủ `/AI/Chat`. Quick action “Làm bài Reading như thế nào?” tạo một hàng người dùng và một câu trả lời. Nhãn `Đã trả lời từ nguồn được phép xem`. Liên kết nguồn trỏ `/Home/Reading`, chữ “Reading”. Chỉ báo đang gõ ở trạng thái ẩn. URL thành `http://localhost:5186/AI/Chat?conversationId=1020019f-5da2-40d3-a0bb-efcbfee85a29`.

Tải lại đúng URL đó vẫn còn câu trả lời, vùng “Nguồn và trạng thái câu trả lời”, cùng nhãn và cùng liên kết Reading.

Embed cùng hội thoại, `/AI/Chat?embed=true&conversationId=1020019f-5da2-40d3-a0bb-efcbfee85a29`, cho cùng câu trả lời, cùng nhãn và cùng liên kết.

Gửi “phase6 double enter” bằng hai lần `requestSubmit` và Enter trong lúc fetch bị giữ. Trạng thái pending là `sending`. Chỉ một bong bóng người dùng mới. Sau khi thả, câu trả lời là dạng chưa đủ căn cứ, nhãn bắt đầu bằng `Chưa đủ căn cứ`.

Stop khi `dataset.streaming=true` và nút dừng đang bật, với nội dung `phase6 stop browser`. Ô soạn thảo được trả lại đúng chữ đó, trạng thái về idle, không có hàng trợ lý sau hàng người dùng, không có đoạn chữ chưa hoàn tất.

Embed mới, fetch bị thay bằng HTTP 429. Bản nháp `phase6 retry same` được khôi phục. Dòng trạng thái: `Bạn đang gửi quá nhanh. Vui lòng chờ một chút rồi thử lại.` Nút `Thử lại` hiện. Lần đầu, trước khi sửa, bấm thử lại tạo hai bong bóng cùng chữ. Sau khi script bỏ qua `#typing` và trình duyệt tải lại script không dùng cache, lần thử lại giữ một bong bóng và xóa ô nhập. Phản hồi của lần này là JSON giả trong trình duyệt.

`loadOlderMessages` chạy trên trang thật với fetch giả. Tiêu đề dài và chuỗi `<img src=x onerror=alert(1)>` hiện thành chữ. Route `javascript:`, `https://` và `/Admin` không thành liên kết. Route `/Home/Study/301` thành liên kết. Tiêu đề rỗng thành `Nguồn đã lưu`. Ảnh phóng 200% và khung 390px cho thấy tiêu đề xuống dòng, ô soạn thảo và nút Gửi vẫn trong vùng nhìn.

Tab đưa `document.activeElement` tới `a.ai-chat-source-link`. Cùng lúc `element.matches(':focus')` trả false và `getComputedStyle` báo outline `none`. Rule CSS `a.ai-chat-source-link:focus, a.ai-chat-source-link:focus-visible` có trong file. Vòng focus nhìn thấy được chưa được xác nhận.

## Hành trình chưa làm

| Hành trình | Việc đã có | Việc chưa có trên trình duyệt |
| --- | --- | --- |
| Trang cũ nhiều trang | Node C4/D3 và fetch giả trong trình duyệt | Bấm tải tin thật trên hội thoại nhiều trang |
| Thu hồi nguồn, dòng legacy | Test C# encode và cache | Thu hồi trên MariaDB rồi tải lại trang |
| 429, timeout, mất mạng của provider | Fetch giả 429 và `Failed to fetch` | Request thật tới loopback. Log loopback trống vì câu trả lời cục bộ không gọi provider |
| A sang B, hạ quyền, gỡ thành viên, cache ấm | `WarmCache_DoesNotLeakAcrossAccountDowngradeOrRevoke` trên SQLite | Chưa đăng xuất sang tài khoản khác, chưa hạ role, chưa gỡ lớp |

Hàng cuối là hạng mục bắt buộc còn thiếu. Vì vậy Phase 6 là BLOCKED, không phải AWAITING_REVIEW.

## Dữ liệu thử còn lại

Hội thoại `1020019f-5da2-40d3-a0bb-efcbfee85a29` còn trên MariaDB local. Có thể xóa bằng nút xóa hội thoại sẵn có. Không reset database và không đụng tài khoản khác.
