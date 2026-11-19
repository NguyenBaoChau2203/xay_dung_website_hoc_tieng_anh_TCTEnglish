# Hội thoại giọng nói ngay trong chatbot — 19/11/2026

Yêu cầu mới của owner thay cách vào voice của plan ngày 18/11: bấm luyện hội
thoại phải đổi giao diện trong chatbot hiện tại, không điều hướng hay mở tab.
Một agent thực hiện trên feature/ai-voice-conversation; toàn bộ local, chưa commit.

## Kết quả và tái sử dụng

- _ChatShell dùng chung cho full chat và iframe có hai nút chế độ. Text DOM,
  draft, tin nhắn, stream, lịch sử và IDs giữ nguyên khi ẩn/hiện. Lịch sử chat chữ
  tạm ẩn ở voice để dành diện tích, hiện lại khi quay về text. Không đổi URL/tab.
- AiVoiceWorkspaceViewComponent chỉ lấy khả năng cấu hình hiện có, truyền typed
  VoicePageVm vào _Workspace. View riêng cũ và cả hai dạng chat dùng một workspace.
  Inline là thuộc tính ViewModel, không phải persisted model; không cần migration.
- Giữ nguyên authenticated APIs, entitlement, antiforgery/ownership, quota,
  WebRTC, sideband ACK, history và cleanup. Đổi mode không xin micro hay tạo call.
  Trở về text chờ xác nhận đóng; nếu lỗi, giữ voice/Stop để thử lại cùng phiên.
- Chat nổi cấp microphone cho iframe cùng origin; đóng khung gửi thông báo tới
  đúng origin, child kiểm tra cả origin và source. Micro/peer/SSE đóng qua
  coordinator hiện có. Mở lại không tự bật micro và giữ bản nháp text.
- Modal chủ đề đưa ra body của document hiện tại, có Bootstrap bundle local cho
  embed. Footer điều khiển ghim ngoài vùng cuộn; khi Start, cuộn tới cáo/trạng thái.
  Thông báo tính trọn thời gian giữ chỗ vẫn hiện trước Start. Transcript, private
  topics và history đều thao tác trong cùng khung.
- Cáo dùng atlas mới sáu biểu cảm tạo bằng built-in ImageGen theo ảnh owner:
  sẵn sàng, lắng nghe, suy nghĩ, nói, hoàn tất, kết nối lại/tắt micro. State dựa trên
  coordinator và playback, không đoán từ animation. Nhún nhẹ khi nói; reduced
  motion tắt chuyển động. Artwork trang trí, status text là thông tin accessible.

## Ảnh và phạm vi ảnh hưởng chung

Asset: `TCTEnglish/wwwroot/brand/mascot/voice/tico-voice-expressions.png`,
1536×1024 RGBA, 3×2 ô bằng nhau, alpha thật đã kiểm tra; 1.88 MB, một atlas cache
dùng cho tất cả biểu cảm. SHA256:
`88319dbff0f8bafc9c7aa47cbcb3ab33d0afb1d280e4885a425068b5e8ed93cb`.
Prompt nguyên văn/provenance và bảng ô ở README cùng thư mục. Không thay asset
mascot cũ, không dùng CLI/key cho ImageGen, không thêm package/CDN/font/palette.

Consumers: _ChatShell → Chat/ChatEmbed; _AiChatLauncher → learner layouts đã có.
Launcher giữ nút mở trang chat đầy đủ riêng; nút luyện voice không dùng liên kết.
Giữ các selectors/IDs hiện có. CSS mới ở ai-voice.css chỉ áp dụng voice và các
panel/mode chat mới; không sửa token CSS/layout nền tảng. API/business/schema,
Program.cs/appsettings/.csproj không thay trong đợt này. Static brand asset thuộc
source; không sửa nội dung catalog/public packages hoặc cần export learner data.

## Bằng chứng

- Node mode regressions: fail trước khi có controller, sau đó pass. Test gồm vào
  voice không gọi provider, chờ đóng xác nhận, dedupe exit, lỗi/timeout giữ Stop,
  thử lại và chặn mode không hỗ trợ.
- .NET Release: **75 passed, 0 failed, 4 MariaDB skipped**, tổng 79; gồm hai render
  cases full/embed, unique text/voice IDs, typed component và không có voice href.
  Rerun riêng 2 render cases pass sau sửa analyzer warning của test. App build
  thành công; warnings sẵn có ngoài phần triển khai mới còn tồn tại.
- Node voice/chat/source/recorder regressions: **66 passed**, không fail/skip.
  Syntax ai-chat-launcher, ai-chat-mode, ai-voice-page và git diff --check pass.
- Browser Chrome: **8 nhóm kiểm tra, 0 page errors**, 3 fake session creates,
  5 close requests giả lập, **0 real session creates/paid Realtime calls**.
  Đăng nhập thật bằng local account đã lưu; API/media/peer/SSE voice được chặn
  và giả lập, không ghi/transmit micro thật, không sửa dữ liệu học hay User Secrets.
- Quan sát 1440 sidebar mở/thu, 768, 390 và 375; không tràn ngang. Footer nhìn
  thấy và hit-test không bị che, kể cả chat nổi/mobile. Các state ready/listening/
  thinking/speaking/muted/error, pending cleanup và retry; frame/tab URL không đổi;
  draft/history text nguyên vẹn, denied mic không tạo call; custom CRUD/history
  read/delete giả lập; Standard lock và unavailable Start gating hoạt động.
- Parent-close tắt track; forged source/origin không đóng phiên. Tab ở trong
  iframe và modal, Escape chỉ đóng modal, đóng/mở launcher giữ draft. Đã sửa hai
  lỗi phát hiện bằng browser: instanceof dùng sai realm; Escape nổi tới launcher
  sau khi Bootstrap xóa .show. Tab ở cuối document iframe cần wrap tại modal vì
  focusin của Bootstrap không bắt được focus đã ra ngoài document.
- Một lần timeout do fake SSE event có thể chạy sau event kiểm tra; fixture chờ
  snapshot idle trước khi inject state. Hit-test ban đầu nhắm button disabled
  (pointer-events:none) được sửa để kiểm footer cha khi nút disabled. Không sửa
  code app để chiều các lỗi fixture này.
- Reduced motion xác minh animationName=none. Thử Ctrl+= năm lần trong headless
  không thay innerWidth/DPR, vì vậy **native zoom 200% chưa quan sát**, không tính
  là pass. Chưa lặp live provider/physical mic/quality acceptance cho inline UI.
  Bằng chứng real-provider trước đó được ghi riêng trong ledger 18/11.

Artifacts local ignored: `.appdata/local/ai-voice-tests/inline-browser-evidence.json`,
`inline-*.png`, `inline-dotnet.log`, `inline-render.log`. Screenshot desktop,
mobile và floating speaking đã xem trực tiếp. Web launcher đúng workflow chạy
5107, giữ preview 180 Standard/900 Admin-Premium giây/ngày, 60 giây/phiên,
concurrency=1; chỉ child env. Health/readiness được xác nhận trước bàn giao.

Không commit, push, PR, CI, staging hay production; không reset DB/quota/refund,
không thêm migration, đổi key hay export private data.
