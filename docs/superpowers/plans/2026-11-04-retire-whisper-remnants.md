# Dọn phần Whisper còn sót — 04/11/2026

Owner yêu cầu loại bỏ Whisper khỏi luồng chạy web local. Một agent, local;
không paid API, không commit/push/CI remote/deploy. Giữ dữ liệu DB, uploads,
API key, history/provenance và bản ghi thử audio hiện có.

- [x] Rà source/DI, launcher, SETUP-LOCAL, IDE tasks/rules và process trên máy.
- [x] Xác nhận launcher không gọi engine; không có Whisper process. Còn helper
  test loopback cũ và artifacts/state trong .appdata/local/speech.
- [x] Xóa helper/test loopback không còn caller production; giữ kiểm tra rằng
  cờ engine cũ không thể bật paid OpenAI test.
- [x] Làm rõ rule hiện tại, đánh dấu prompt/handoff Whisper cũ đã superseded.
- [x] Xác minh path/reparse/process; xóa vĩnh viễn cache bị automatic policy
  review chặn cả với literal path. Đã đổi tên nguyên thư mục thành
  `.appdata/local/retired-speech-2026-11-04` để bỏ khỏi vị trí runtime, có thể
  hoàn tác và giữ audio/logs. 43 artifacts/109780875 bytes vẫn nằm trong archive,
  không báo đã xóa/giải phóng disk. Sáu hash evidence giữ nguyên.
- [x] Launcher fixture với poison legacy script/process/download; xác minh
  ordinary startup vẫn MariaDB + web, không chạy/download local ASR.
- [x] Full .NET890/0/12, Python Ran99/skipped1, tooling/setup9/9/native hooks;
  diff check; ghi evidence
  và cập nhật bug memory/known issues.

Không xóa nhãn dữ liệu lịch sử `whisper-audio`, tài liệu lịch sử hoặc font
Whisper trong design reference: chúng không phải engine hay startup behavior.
