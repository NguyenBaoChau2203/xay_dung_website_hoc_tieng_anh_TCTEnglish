# Hội thoại AI bằng giọng nói

Ngày 18/11/2026. Tính năng local trên `feature/ai-voice-conversation`.
Mặc định **tắt**. Đã kiểm tra một cuộc hội thoại qua API thật bằng câu nói tổng hợp
trên local; chưa nghiệm thu micro vật lý, chất lượng nghe hoặc Plesk.
Xem [implementation ledger](../feature-audits/2026-11-18-ai-voice-conversation-implementation-ledger.md)
để phân biệt kiểm tra tự động, browser giả lập provider và live acceptance.

## Cấu hình và phạm vi

Trong chat đầy đủ hoặc chat nổi, nút luyện hội thoại đổi chế độ ngay trong cùng
khung, không đổi URL hay mở tab. Bản nháp/lịch sử chat chữ giữ nguyên. Chuyển về
chat chữ chờ xác nhận đóng voice; lỗi đóng giữ nút Dừng phiên để thử lại. Đóng
chat nổi tắt micro qua thông báo cùng origin. Vào chế độ voice chưa tự bật micro.
`/AI/Voice` vẫn có cho bookmark cũ và dùng chung workspace; các flow yêu cầu đăng nhập.
Chi tiết UI/mascot và kiểm tra: [inline audit](../feature-audits/2026-11-19-inline-ai-voice-chat.md).
Chat chữ/RAG và recorder bài nói giữ
pipeline hiện có. Giọng nói dùng audio-only WebRTC trực tiếp tới OpenAI;
backend .NET quản lý cấu hình qua sideband WSS, quota, transcript và hangup.
Không có Node/Python service mới, codec/VAD/STT/TTS tự viết hoặc raw audio storage.

Cập nhật theo yêu cầu owner 19/11/2026: **nói lần lượt, không ngắt lời AI bằng
giọng nói**. Server VAD giữ tự trả lời nhưng đặt `interrupt_response=false`.
Browser tạm ngừng gửi tiếng micro trong lúc kết nối, AI chuẩn bị/trả lời; chỉ
mở lại khi nhận trạng thái lắng nghe sau `output_audio_buffer.stopped`, không
dùng `response.done` làm dấu kết thúc phát tiếng. Tắt micro thủ công vẫn được giữ
khi sang lượt mới. Snapshot SSE có chu kỳ 700ms nên đèn trạng thái/chuyển micro có
thể trễ tương ứng; bảo vệ không ngắt câu ở provider không phụ thuộc độ trễ đó.
Sự kiện VAD còn sót khi đang playback không tự đánh dấu transcript bị ngắt.
Dừng phiên vẫn kết thúc ngay âm thanh/micro và chạy quy trình đóng phiên hiện có.
Các dấu cleared/truncated/cancelled và lịch sử phiên cũ vẫn được bảo toàn.
Chi tiết: [sequential-turn plan](../superpowers/plans/2026-11-19-voice-sequential-turns.md).

Model lập trình do owner chọn trong IDE, model chat chữ và model voice là ba lựa
chọn độc lập. Không đổi `AI:Model` (`gpt-6-luna`) hoặc reasoning của chat chữ.
Voice mặc định `gpt-realtime-2.1`, voice `marin`, protocol **Realtime GA**.
Input transcript dùng `gpt-transcribe` theo audio.input.transcription trong
[Realtime session schema](https://developers.openai.com/api/reference/resources/realtime/client-events).
Transcription này chỉ hỗ trợ history; phản hồi bằng audio vẫn là speech-to-speech.
Không gửi gợi ý ngôn ngữ cố định: provider tự nhận diện ngôn ngữ. Kiểm tra local
phát hiện ACK không echo `audio.input.transcription.language` đúng như cấu hình
đã yêu cầu; gửi `language=en` rồi kiểm so sánh nguyên trạng làm setup timeout.
Model, instructions, tools, VAD, giọng và các cấu hình còn lại vẫn được xác nhận.
Ngày kiểm tra tài liệu, model `gpt-realtime` cũ được đánh dấu deprecated; model
mới và unified call/sideband được mô tả trong [model reference](https://developers.openai.com/api/docs/models/gpt-realtime-2.1)
và [WebRTC Realtime](https://developers.openai.com/api/docs/guides/voice-webrtc?api=realtime).
Không trộn GPT-Live hoặc Realtime beta vào adapter này.

Options `AI:Voice` (có thể cấp bằng child-process env, không cần sửa appsettings):

| Option | Default | Ý nghĩa |
| --- | --- | --- |
| Enabled | false | Chỉ bật sau khi owner cho phép live với budget hữu hạn. |
| Model | gpt-realtime-2.1 | Đã dùng thành công trên account local hiện tại; không chứng minh account/hosting khác. |
| Voice | marin | Giọng nói provider; không phải voice clone. |
| StandardDailySeconds / StandardSessionSeconds | 180 / 180 | Hạn mức riêng với câu hỏi chat chữ. |
| PremiumDailySeconds / PremiumSessionSeconds | 900 / 600 | Áp dụng cả Admin/legacy Premium theo DB entitlement. |
| MaxConcurrentSessions | 10 | Global capacity bền vững; một lease/user. |
| SetupTimeoutSeconds | 30 | Deadline setup; provider HTTP 15s, ACK sideband 10s. |
| IdleTimeoutSeconds | 60 | Đóng khi không có hoạt động. |
| CleanupMaxAttempts | 8 | Retry tự động có backoff; giữ trạng thái pending khi hết lượt. |

Adapter dùng resolver key speech hiện có: cấu hình speech phải hợp lệ/enabled;
resolver ưu tiên speech-specific key, rồi AI key, rồi fallback env theo source
hiện hành. Endpoint voice cố định `api.openai.com`, không dùng URL client đưa lên.
Không ghi/sao chép User Secrets, không gửi key/call ID cho browser, không log SDP,
raw provider events, instructions hoặc provider error. Named HttpClient tắt redirect
và HTTP logging. `scripts/speech-status.ps1` chỉ chứng minh cấu hình local;
`Configured=true` không chứng minh quyền Realtime, quota hoặc tài khoản trả phí.

Khởi động đúng workflow:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5107
```

Giữ process đang chạy; chọn cổng trống. Kiểm tra `/health/ready` và speech status.
Ordinary startup/GET trang không tạo call, không thu audio hoặc chạy bài test có phí.
Worker chỉ đóng/recover call đã có evidence trong DB; không tạo call thay thế.
Không dùng biến bật live khi chạy test thông thường.

## Dữ liệu, entitlement và allowance

Standard dùng catalog đã published; Premium/Admin/legacy Premium có thể tạo,
sửa và bắt đầu chủ đề riêng. Trước gate xử lý subscription expiry và kiểm DB;
cookie Premium cũ không cấp quyền. Role Premium có lịch sử subscription bị
expired/revoked cần một subscription active hiện tại. Worker recheck trong phiên
custom, đóng khi downgrade/revoke/block/anonymize. Chu kỳ kiểm tra là 5 giây,
không phải cam kết thu hồi tức thì. Sau downgrade chủ sở hữu vẫn đọc/xóa chủ đề
riêng và lịch sử; người khác nhận 404.

Mỗi lần Start reserve window trước provider request. Window là min(hạn mức còn,
max/session, thời gian tới UTC midnight). Call được provider chấp nhận tiêu thụ
toàn bộ window đã dành, kể cả dừng sớm; UI thông báo trước khi bật mic. Đây là
allowance bảo thủ, không phải hóa đơn provider hoặc đo thời lượng audio chính xác.
Input/output audio/text tokens được ghi riêng theo response.done đã dedupe.
Usage quan sát qua sideband có thể thiếu khi mất kết nối; không thay provider billing.

Một hàng capacity khóa transaction ngắn cho lease/quota, hoạt động giữa nhiều
connection/process. Không giữ DB transaction trong network request. Các reservation
của một user được giới hạn 5 lần/phút trước provider I/O. Unknown outcome giữ
reservation/lease pending; không refund theo timeout của browser.
HTTP 4xx provider từ chối rõ ràng (trừ 408) được xử lý như không tạo call.
Trường hợp dispatch mất response/Location hoặc HTTP 5xx không được suy đoán là
không tính phí. UTC ngày mới không làm biến mất reservation cũ hoặc lease pending.

History lưu snapshot chủ đề và finalized transcript. Không lưu từng audio delta.
Assistant bị ngắt/truncated/Stop khi còn playback được đánh dấu có thể chứa phần
chưa nghe; không khẳng định toàn bộ text đã được phát. Xóa history xóa transcript
và snapshot; metadata cleanup vẫn tồn tại. Xóa/anonymize account dùng transaction
hiện có: xóa private rows, detach UserId, giữ call ID/deadline cho worker; không
cascade mất call đang cần hangup.

## Catalog public

Catalog dùng typed registry `AiVoiceCatalogTopic`, int root ID. Sáu chủ đề curated:
làm quen, café, du lịch, daily routine, sở thích, phỏng vấn. Seed thêm theo stable
key và không ghi đè bản sửa local. Chỉ chạy seed chủ động, không seed lúc mở trang.
Lệnh CLI `--shared-data=seed-voice-catalog --shared-target=local` dùng guard Docker
local hiện có; chạy từ thư mục TCTEnglish với connection local trong process.

Workflow bắt buộc: Status → seed/author → Export đúng root → review package/hash
→ Validate → Sync. Dùng `scripts/shared-data.ps1` và [shared-data workflow](shared-data-workflow.md).
Sáu package `team.voice.*` version 1 được export cùng source. Không sửa package
đã published. `AiVoiceCustomTopic`, sessions, turns, receipts, leases, usage và user
không là kiểu shared allowlisted, bị từ chối export. Không thêm chúng vào registry.

## Stop, recovery và giới hạn vận hành

Sau Bắt đầu, chờ “AI đang lắng nghe” rồi nói. Nói xong chỉ cần ngừng một chút;
VAD tự kết thúc lượt và yêu cầu AI trả lời. **Dừng phiên kết thúc toàn bộ cuộc
trò chuyện**, không phải nút gửi câu vừa nói. Tắt micro dùng để tạm ngừng thu tiếng.

Browser Stop đóng track/peer/SSE ngay, gửi close bằng antiforgery. HTTP 202 nghĩa
cleanup đang pending; trang giữ ID, khóa Start và tự kiểm tra cùng phiên trong
tối đa 30 giây. Nếu vẫn chưa xác nhận hoặc mất mạng thì cho thử lại Dừng phiên. HTTP 200
với `closed=true` mới xác nhận cleanup. Late mic permission tự stop track; không
tự tạo paid call khi SSE/ICE/playback mất kết nối. Cancellation tombstone ngăn
setup cũ quay lại sau Stop, kể cả trước lúc reserve. Không tự xóa tombstone để thử
lại cùng ID; phiên mới dùng UUID mới khi lease đã được đóng.

Worker dựa vào trạng thái DB, heartbeat 5s và dead-process cutoff 35s; setup
abandoned chỉ finalize sau ít nhất 40s. Hangup cần provider 2xx/404; timeout/5xx
giữ call ID, reservation và lease. Bounded claim tránh hai worker hangup đồng thời.
Sau restart worker xử lý metadata cũ, không attach lại để mở một paid call mới.
Hết retry phải kiểm tra thủ công qua operator workflow được duyệt. Không sửa
usage/lease/history để giả xác nhận đóng. Không có nút Admin reconcile v1.

Nếu tạo call thành công nhưng DB không ghi được Location, adapter thử hangup
ngay bằng timeout riêng. Nếu DB và hangup đều hỏng, reservation có thể chỉ còn
unknown evidence mà thiếu call ID. Cần đối soát provider thủ công. Direct WebRTC
không bảo đảm hangup ngay khi app bị kill, mất mạng provider hoặc hết retry;
quota nội bộ không phải hard monetary cap. Không tuyên bố đã giải quyết giới hạn
này bằng timer browser. Chưa có retention/purge job tự động; private history giữ
đến khi owner xóa; closed cleanup/tombstone giữ để chống replay.

Hosting còn phải xác minh: HTTPS/micro permissions, outbound HTTPS/WSS, IIS
unbuffered SSE/request duration, app pool luôn chạy, shutdown timeout, multi-instance
workers và network recovery. Không thay đổi Plesk/gates từ tính năng này.

## Acceptance còn cần quyền riêng

Chưa chạy paid/live microphone acceptance; chưa xác minh account/model access,
chi phí thực tế, 5 lượt hội thoại, 2 lần interruption, âm thanh/mic thật, p50/p95
latency hoặc thời gian dừng âm thanh. Cần owner cấp quyền cùng budget hữu hạn
trước lần gọi. Browser fake và xUnit không thay cho các bằng chứng đó.
Chưa chạy CI/staging/production; không commit/push/PR/deploy trong task này.
