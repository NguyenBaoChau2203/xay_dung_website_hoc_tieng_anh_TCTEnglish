# Hướng dẫn Nhận diện Thương hiệu AI Chatbot — TCT AI

Tài liệu hướng dẫn nhận diện và sử dụng biểu trưng chính thức cho trợ lý ảo **TCT AI** (TCT English AI Chatbot). Đây là bộ nhận diện chuyên biệt cho tính năng AI học tập, đồng bộ cùng hệ thống Claymorphism và bảng màu thương hiệu tại [DESIGN.md](../../DESIGN.md).

---

## 1. Ý nghĩa biểu trưng & Hướng thiết kế được chọn

### Ý tưởng thiết kế: "TCT Companion Beacon"
- **Thực thể biểu trưng**: Gương mặt trợ lý AI thân thiện được lồng ghép trong hình khối bong bóng thoại (speech-bubble squircle).
- **Ý nghĩa chi tiết**:
  1. **Hình khối bong bóng thoại bo tròn (Speech Squircle)**: Đại diện cho tính năng giao tiếp, đối thoại thông minh và hỗ trợ học tập liên tục (Vocabulary, Speaking, Writing, Grammar).
  2. **Kính visor và ánh mắt thân thiện**: Tạo cảm giác gần gũi, tin cậy cho học viên, xoá bỏ rào cản sợ sai khi học tiếng Anh.
  3. **Ngọn hải đăng tri thức / Ngôi sao AI 4 cánh vàng (`#ffcd1f`)**: Biểu tượng ánh sáng trí tuệ nhân tạo, điểm tựa khai mở vốn từ vựng và tư duy ngôn ngữ cho học viên TCT English.
  4. **Nút tai nghe thông minh (Communicator Ear Node)**: Thể hiện khả năng lắng nghe và đồng hành trong các bài luyện phát âm, shadowing và hội thoại.

---

## 2. Danh mục tài nguyên (Asset Inventory)

Tất cả tài nguyên chính thức được lưu trữ tại thư mục `TCTEnglish/wwwroot/images/ai/`:

| Tên file | Định dạng | Kích thước | Mục đích sử dụng |
|---|---|---|---|
| `tct-ai-mark.svg` | Vector SVG | 128x128 viewBox (Co giãn vô hạn) | Biểu trưng chính đa sắc, hiệu ứng Clay đổ bóng mềm, dùng cho Chat Header, Launcher Trigger, Assistant Avatar trong tin nhắn web. |
| `tct-ai-mark-mono.svg` | Vector SVG | 128x128 viewBox | Bản đơn sắc (Monochrome / `currentColor`), dùng cho in ấn đơn sắc, văn bản tương phản cao, hoặc giao diện tối giản. |
| `tct-ai-mark.png` | Raster PNG | 512x512 px (Transparent) | Bản xuất độ phân giải cao nền trong suốt, phục vụ hiển thị nhúng ngoài web, tài liệu giới thiệu hoặc fallback khi cần raster. |
| `tct-ai-launcher.png` | Raster PNG | Lưu trữ kế thừa | File ảnh tạm cũ được giữ lại để đảm bảo tương thích ngược tuyệt đối với các bản ghi lịch sử / cache cũ, không làm đứt gãy 404. |

---

## 3. Màu sắc quy chuẩn

Tuân thủ nghiêm ngặt bảng màu thương hiệu tại `DESIGN.md`:

| Thành phần | Mã màu hex | Vai trò / CSS Token tham chiếu |
|---|---|---|
| **Thân trợ lý (Gradient)** | `#556bff` → `#4255ff` → `#2136e7` | Xanh chủ đạo thương hiệu (`--tct-primary`, `--tct-primary-strong`) |
| **Kính visor gương mặt** | `#ffffff` viền `#d5ddff` | Nền trắng thuần khiết (`--tct-surface`) kết hợp sắc thái dịu |
| **Ngôi sao trí tuệ (Spark)**| `#fff176` → `#ffcd1f` → `#f59e0b` | Điểm nhấn vàng TCT (`--tct-accent`, `--tct-warning`) |
| **Mắt & nụ cười** | `#111c2d` | Màu chữ tối tương phản cao (`--tct-text`, tỉ lệ tương phản > 12:1) |
| **Hào quang nền (Ambient)** | `#eef2ff` (Opacity 60%) | Sắc diện pastel dịu mắt (`--tct-primary-soft`) |
| **Gò má cảm xúc** | `#ffcd1f` (Opacity 45%) | Điểm xuyết năng lượng học tập tích cực |

> **Lưu ý**: Bản vector master là file độc lập tự chứa mã màu; không đưa thêm mã màu mới vào `:root` của CSS hệ thống.

---

## 4. Quy chuẩn hiển thị & Kích thước sử dụng

### Khoảng cách an toàn (Clear Space)
- Khoảng cách an toàn tối thiểu xung quanh biểu trưng bằng **20% chiều rộng biểu trưng** (khoảng `0.2W`).
- Không để chữ, viền layout hoặc nút bấm khác dính sát vào đường bao của tia sao hoặc đuôi bong bóng thoại.

### Kích thước tối thiểu khuyến nghị
Kết quả thực nghiệm trên các kích thước hiển thị:
- **64px**: Kích thước lý tưởng trên nút bấm nổi **Floating Launcher Trigger** (`.ai-launcher-trigger`).
- **40px - 48px**: Kích thước chuẩn trên **Chat Shell Header** và **Modal Dialog Header**.
- **32px**: Kích thước chuẩn cho **Assistant Message Avatar** (`.ai-chat-avatar-assistant`) trong danh sách tin nhắn. Mắt, nụ cười và ngôi sao vàng hiển thị rõ ràng, sắc nét.
- **24px**: **Kích thước tối thiểu tuyệt đối (Minimum Usable Size)**. Ở 24px (ví dụ icon nhúng trên thanh tiêu đề di động), hình khối đầu trợ lý và ngôi sao vẫn phân biệt rõ rệt. Không dùng biểu trưng ở kích thước dưới 24px vì chi tiết mắt/miệng sẽ bị suy giảm độ tương phản.

### Cắt cúp (Crop & Placement)
- **Cắt tròn (Circular Crop)**: Sử dụng trong `.ai-chat-avatar-assistant` với nền xám nhạt/trắng bo tròn 50%. Cần chừa padding nội bộ tối thiểu `10%` để đỉnh tia sao vàng không bị lẹm vào mép viền tròn.
- **Cắt bo góc squircle (Clay Card Crop)**: Sử dụng trong launcher button với bo góc mềm (`border-radius: 999px` hoặc `18px`).
- **Phân biệt rõ ràng**: Biểu trưng **TCT AI** chỉ đại diện cho trợ lý hệ thống; **tuyệt đối không dùng thay thế cho avatar người dùng / học viên**.

---

## 5. Nguồn gốc, Công cụ & Bản quyền (Provenance)
- **Tác quyền**: Sáng tạo nguyên bản bởi đội ngũ phát triển TCT English, phục vụ độc quyền cho nền tảng TCT English.
- **Công cụ tạo lập**: Vector SVG tinh chỉnh trực tiếp theo toạ độ toán học, kiểm thử hiển thị bằng engine Chromium không phụ thuộc thư viện raster bên thứ ba.
- **Độ sạch mã nguồn**: Không chứa mã thực thi Javascript, không dùng `foreignObject`, không phụ thuộc font chữ nhúng bên ngoài, đảm bảo an toàn tuyệt đối theo tiêu chuẩn Content Security Policy (CSP).
