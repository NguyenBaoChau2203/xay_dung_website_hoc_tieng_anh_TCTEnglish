# Dọn dấu vết Whisper khỏi luồng local — 04/11/2026

Owner yêu cầu kiểm tra và loại bỏ Whisper từng chạy cùng web. Một agent,
local-only, giữ dirty work/branch `feature/chau-ui-performance-audit`. Không
commit/push/remote CI/merge/deploy, không paid API hay thay dữ liệu/key.

## Kết quả kiểm tra ban đầu

- `scripts/start-local.ps1`, SETUP-LOCAL.cmd và IDE task đã đi qua MariaDB + web,
  không còn gọi local-speech.ps1, tải model hay start Whisper.
- Program.cs đăng ký ISpeechTranscriber → OpenAiSpeechTranscriber, options chỉ
  còn OpenAI. Không có runtime Whisper reference trong application source.
- Không thấy process Whisper trên Windows, không thấy task khởi động Windows
  hoặc Scheduled Task khớp Whisper/local-speech. Không có key cấu hình Whisper/
  ModelIdentity cũ trong User Secrets; không có biến môi trường Whisper/ASR cũ
  ở Process/User/Machine. Không có lệnh whisper/whisper-cli/whisper-server trên PATH.
- Còn AsrLiveAcceptanceFactAttribute và bộ test parser loopback cũ; chỉ gọi lẫn
  nhau, không còn live test tiêu thụ attribute đó.
- Còn43 managed artifacts trong `.appdata/local/speech/artifacts`, tổng
  **109,780,875 bytes (~104.7 MiB)**, stale state/lock và6 audio/log evidence files.
- Ba prompt Whisper và hai tài liệu Speaking cũ còn chỉ dẫn khôi phục engine/
  Gemini nếu đọc tách khỏi quy định mới. AGENTS còn hardcode tên branch Whisper cũ.

## Thay đổi

1. Xóa helper/test loopback cũ:
   `TCTEnglish.Tests/Infrastructure/AsrLiveAcceptanceFactAttribute.cs` và
   `TCTEnglish.Tests/AsrLiveAcceptanceConfigurationTests.cs`.
2. Thay bằng `OpenAiSpeechLiveConfigurationTests.cs`: cờ loopback cũ không được
   bật paid OpenAI test; chỉ exact opt-in mới của speech mới bật. Test chỉ kiểm
   metadata/flag, không tạo transcriber hay gọi HTTP. Env được phục hồi sau test.
3. Củng cố fixture `scripts/tests/test_local_launcher.py` với poison old script,
   cache/state cũ, Start-Process và Invoke-WebRequest traps. Ordinary startup,
   startup failure và busy-port vẫn đạt; không thể nhận pass nếu gọi engine/
   download/background process qua các đường này.
4. AGENTS.md và .ai/README.md cấm khôi phục local engine từ plan/prompt/cache
   lịch sử. Bỏ branch hardcode. Ba prompt `.ai/prompts/cursor-auto-whisper-*`
   và hai premium Speaking plan/handoff được gắn banner superseded.
5. Hướng dẫn local-speech ghi rõ cache cũ không được đọc/start; giữ lịch sử,
   recording/logs và transcript provenance.

## Cache trên máy và giới hạn policy

Automatic policy review từ chối xóa vĩnh viễn cả lệnh cleanup tổng hợp và lệnh
Remove-Item vào literal absolute artifacts path; thông báo chỉ nêu
**`blocked by policy`**, không có lý do chi tiết. Không đổi shell/tool khác để
vượt việc chặn xóa.

Đã dùng phương án có thể hoàn tác: xác minh cả source/destination tuyệt đối nằm
trong checkout, không reparse points ở đường dẫn, destination chưa tồn tại và
không process dùng cache; rồi đổi tên nguyên thư mục:

    .appdata/local/speech
    → .appdata/local/retired-speech-2026-11-04

Đường dẫn runtime cũ **không còn**; archive bị Git ignore. Toàn bộ43 artifact
files vẫn được lưu, **chưa giải phóng105 MiB disk**. State/lock/log/probes được
giữ trong archive, không có launcher/task tham chiếu archive. Sáu SHA256 của
audio/log evidence trước/sau giữ nguyên. README trong archive ghi rõ retired,
không được execute hoặc phục hồi startup. DB/uploads/API secrets không thay đổi.

Evidence ignored: `.tmp/whisper-retirement/cleanup.json` và evidence-before.json.

## Những chữ Whisper được giữ có chủ đích

- Nhãn lịch sử `whisper-audio` và fixture compatibility: giữ khả năng đọc dữ liệu.
- Bug logs/audit/plans cũ: lịch sử đã thực hiện, prompt cũ có banner superseded.
- Guard/regression chứng minh engine cũ không chạy: không phải runtime caller.
- Font Whisper trong design reference: tên font, không liên quan ASR.

Không xóa dữ liệu/history để làm kết quả tìm từ khóa bằng0. Rà lại application,
scripts, native configs/tasks, CI và startup wrappers không có active engine
call/download. Các rule nhắc Whisper hiện tại là lệnh cấm phục hồi.

## Verification

| Check | Kết quả |
| --- | --- |
| Launcher fixture có poison legacy script/cache/process/download | **4/4 pass** |
| Full .NET optimized SpeechReview | **890 passed /0 failed /12 skipped**, tổng902 |
| Python script discovery | **Ran99, skipped1** =98 executed pass /0 fail |
| Native setup tests | **9/9 pass** |
| agent_tooling.py | verified |
| check_team_hooks.py | Four native host commands / four events / malformed input: PASS |
| Retired script, artifact lock và active cache path | Đều absent |
| Speech metadata sau archive | configured, gpt-transcribe, User Secrets AI:ApiKey; VerifiedLive=false |
| git diff --check | sạch; CRLF warnings ở UI dirty có sẵn |

Thay12 test parser engine đã xóa bằng5 test opt-in mới; vì vậy tổng suite giảm7
so với lượt review trước, không phải bỏ business regression hay chuyển fail
sang skip. Live/provider/scale checks không được bật. Không cần chạy lại UI/JS
vì không đổi application/UI runtime trong lượt cleanup này.

Docker Desktop Linux Engine hiện không kết nối được (named pipe không tồn tại),
nên không xác minh danh sách container và không khởi động web thật trong phiên
này. Không suy từ launcher fixture rằng đã có end-to-end web/DB readiness mới.
OpenAI account/quota/mic thật vẫn là gate riêng; ordinary startup không gọi API.
