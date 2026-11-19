# Khảo sát tái sử dụng cho hội thoại AI bằng giọng nói

Ngày khảo sát: 18/11/2026. Nhánh: `feature/ai-voice-conversation`.
Phạm vi hiện tại: khảo sát, thiết kế và tài liệu; chưa cài dependency, nhập mã
nguồn bên thứ ba, gọi API trả phí hoặc triển khai tính năng.

## 1. Yêu cầu và kết luận đề xuất

Châu đã xác nhận hội thoại trực tiếp: người dùng nói, AI tự nhận biết khi người
dùng ngừng nói, trả lời bằng giọng nói và cho phép người dùng ngắt lời AI. Người
dùng chọn chủ đề có sẵn; chỉ Premium được tạo chủ đề riêng. Model của agent lập
trình là **GPT 6.1 Sol / High**; đây không phải cấu hình model của website.

Nguồn phù hợp nhất tìm được là ví dụ chính thức
[OpenAI realtime-server-controlled](https://github.com/openai/openai-agents-js/tree/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled).
Đề xuất tái sử dụng helper âm thanh và coordinator của ví dụ, chuyển các cơ chế
kiểm soát phía server sang ASP.NET Core, kết nối OpenAI Realtime. Không mang cả
monorepo, giao diện demo, demo auth, Node server hay Vite vào ứng dụng đang chạy.

Đây là lựa chọn kiến trúc đề xuất sau khảo sát, chưa phải bằng chứng tương thích
đã chạy. Phase 0 của [plan](../superpowers/plans/2026-11-18-ai-voice-conversation-plan.md)
phải xác minh transport, sự kiện, model và điều kiện vận hành trước khi chốt adapter.

## 2. Những nguồn đã kiểm tra

| Nguồn | Đã đọc và khả năng có sẵn | Giấy phép đã kiểm tra | Đánh giá cho TCT English |
| --- | --- | --- | --- |
| [openai/openai-agents-js: ví dụ server-controlled](https://github.com/openai/openai-agents-js/tree/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled) | README, server entry, browser helper/coordinator, SDP policy, readiness và session manager; có WebRTC audio, sideband control, state và cleanup | [MIT](https://github.com/openai/openai-agents-js/blob/1215a24fb4725742c7b88094812b7ba18c46041c/LICENSE) | Nguồn chính để thích ứng. Frontend không đòi hỏi React; backend ví dụ dùng Node và Agents SDK nên cần chuyển phần tích hợp sang .NET. |
| [openai/openai-dotnet](https://github.com/openai/openai-dotnet/tree/c36963d4976eca6ea82e9ff7cf7b2df23da08a44) | Đã đọc `RealtimeSessionClient`; lớp đang có thuộc tính `Experimental("OPENAI002")` ở revision khảo sát | [MIT](https://github.com/openai/openai-dotnet/blob/c36963d4976eca6ea82e9ff7cf7b2df23da08a44/LICENSE) | Là ứng viên cho server adapter nếu xác minh được đúng unified call + sideband `call_id`. Chưa có bằng chứng đó; không thêm NuGet chỉ vì SDK mang tên OpenAI. |
| [pipecat-ai/pipecat](https://github.com/pipecat-ai/pipecat) | README mô tả framework Python, transport WebRTC/WebSocket và pipeline voice có sẵn | [BSD 2-Clause](https://github.com/pipecat-ai/pipecat/blob/main/LICENSE) | Hữu ích khi cần nhiều nhà cung cấp/pipeline tùy biến. Thêm runtime Python và triển khai worker cho nhu cầu hiện tại; chưa chọn làm dependency. |
| [livekit/agents](https://github.com/livekit/agents) | README: voice agent, turn detection, SDK client, triển khai qua hệ sinh thái LiveKit; có nhánh JS/TS riêng | [Apache 2.0](https://github.com/livekit/agents/blob/main/LICENSE) | Hợp khi cần room, nhiều người, telephony hoặc hạ tầng media riêng. Với hội thoại một người–một AI hiện tại, thêm hạ tầng room/agent chưa có trong TCT English. |

Đánh giá mức phù hợp là suy luận từ source/README và kiến trúc hiện tại. Chưa
benchmark độ trễ, chi phí hay xác minh các framework này trên Plesk. Không dùng
số sao GitHub làm chứng cứ tương thích hoặc an toàn. Không cần tích hợp đồng thời
cả bốn nguồn.

## 3. Revision và bản đồ tái sử dụng cụ thể

Revision tham khảo chính: **`1215a24fb4725742c7b88094812b7ba18c46041c`**,
được lấy từ commit của repo OpenAI qua GitHub trong lần khảo sát này. Các liên kết
dưới đây cố định revision; SHA của nội dung file không được dùng thay commit SHA.

Đường dẫn nguồn đều nằm dưới `examples/realtime-server-controlled/`:

| File nguồn | Tái sử dụng/thích ứng vào đâu | Phần cần thay cho TCT English |
| --- | --- | --- |
| [src/client/audioOnlyWebRtc.ts](https://github.com/openai/openai-agents-js/blob/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled/src/client/audioOnlyWebRtc.ts) | `TCTEnglish/wwwroot/js/ai-voice-audio.js` | Chuyển TypeScript thành vanilla JS theo cách đóng gói hiện có; giữ các hành vi track cleanup, lỗi playback, late permission result và disconnect timeout. |
| [src/client/voiceSessionCoordinator.ts](https://github.com/openai/openai-agents-js/blob/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled/src/client/voiceSessionCoordinator.ts) | `TCTEnglish/wwwroot/js/ai-voice-session.js` | Đổi URL, antiforgery, topic/session DTO và thông báo; giữ cơ chế chống race giữa Start/Stop và các phiên. |
| [src/server/sdpPolicy.ts](https://github.com/openai/openai-agents-js/blob/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled/src/server/sdpPolicy.ts) | `Services/AI/Voice/VoiceSdpPolicy.cs` | Chuyển policy sang C#, giới hạn byte và port/media validation tương đương; kiểm cả offer và answer. |
| [src/server/sidebandReady.ts](https://github.com/openai/openai-agents-js/blob/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled/src/server/sidebandReady.ts) | `Services/AI/Voice/OpenAiRealtimeVoiceClient.cs` | Chờ xác nhận cấu hình thực sự của phiên TCT, có timeout/cancellation; không chấp nhận một ACK không liên quan. |
| [src/server/sessionManager.ts](https://github.com/openai/openai-agents-js/blob/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled/src/server/sessionManager.ts) và [server.ts](https://github.com/openai/openai-agents-js/blob/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled/server.ts) | `AiVoiceSessionService`, registry phiên sống và worker cleanup | Thay demo principal bằng user thật; thêm reservation, trạng thái bền vững và recovery sau restart. Không sao chép lưu toàn bộ trạng thái chỉ trong RAM. |
| [src/shared/publicEvents.ts](https://github.com/openai/openai-agents-js/blob/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled/src/shared/publicEvents.ts) | `VoicePublicEvent` và endpoint SSE của TCT | Giữ allowlist; bổ sung transcript của chủ sở hữu khi cần. Không gửi nguyên sự kiện provider. |
| [src/client/main.ts](https://github.com/openai/openai-agents-js/blob/1215a24fb4725742c7b88094812b7ba18c46041c/examples/realtime-server-controlled/src/client/main.ts) | Tham khảo wiring trong `ai-voice-page.js` | Dùng Razor ViewModel, Clay components, cookie auth/antiforgery hiện có; bỏ demo CSS và demo login. |

Các đích trong bảng là **file dự kiến**, chưa tồn tại do lần này chỉ lập kế hoạch.
Khi nhập code, đọc đầy đủ các file được chọn cùng test liên quan tại revision đã
ghim; khảo sát này không khẳng định đã review toàn bộ monorepo. Giữ nguyên license,
copyright và ghi rõ đoạn được chuyển đổi; lưu provenance/checksum của file thực
sự nhập vào một hồ sơ riêng của tính năng. Không sửa lock của tooling AI chỉ để
ghi dependency ứng dụng.

## 4. Vì sao chọn mẫu audio-only có server control

Mẫu được kiểm tra gửi âm thanh qua WebRTC và kiểm soát phiên qua server. Nó kiểm
SDP ở backend để từ chối data channel, thay vì chỉ bỏ lệnh tạo data channel ở JS.
Điều này giúp giữ thao tác sửa phiên ở server. TCT cần giữ nguyên ranh giới đó khi
chuyển ngôn ngữ. Khóa API và provider call ID nằm phía backend.

Các phần cụ thể nên dùng lại gồm kiểm tra SDP, chờ readiness, khóa phiên theo
user, cancellation trước khi setup xong, retry hangup và cleanup của micro/loa.
TCT vẫn phải thêm DB ownership, entitlement, quota và transcript riêng của mình.

Mẫu gốc không bảo đảm cleanup sau khi tiến trình bị dừng cưỡng bức. TCT cần lưu
call/session và công việc cleanup vào DB, xử lý orphan sau restart. Không tuyên bố
giới hạn chi phí tuyệt đối khi server mất kết nối provider. Nếu hosting không đủ
điều kiện cho sideband/SSE/worker, ghi blocker và đề xuất thay đổi vận hành riêng.

## 5. Tham chiếu API chính thức đã mở

- [WebRTC unified interface](https://developers.openai.com/api/docs/guides/voice-webrtc?voice-api=realtime): backend trao đổi SDP với Realtime API; âm thanh đi qua media tracks.
- [Server-side controls](https://developers.openai.com/api/docs/guides/voice-server-controls?voice-api=realtime): server gắn sideband vào cuộc gọi bằng `call_id`.
- [Hang up call](https://developers.openai.com/api/reference/resources/realtime/subresources/calls/methods/hangup): có endpoint kết thúc cuộc gọi từ server.
- [Voice activity detection](https://developers.openai.com/api/docs/guides/realtime-vad): cấu hình nhận biết lượt nói và interruption.

Thực thi phải đối chiếu đúng họ API/model Realtime đã chọn. Không trộn schema của
GPT-Live, Realtime beta và Realtime hiện hành. Model, khả năng tài khoản, hạn mức
và giá tại thời điểm triển khai còn phải xác minh; tài liệu có sẵn không chứng
minh key local được cấp quyền. Không có live call trong lần khảo sát này.

## 6. Phần có sẵn trong TCT English cần dùng lại

- `Controllers/AiController.cs`: `/AI/Chat`, lịch sử/phân trang, quota và
  antiforgery; `AiChatService` đã có ownership/reservation cho chat chữ. Giữ
  nguyên HTTP/JSON/SignalR contracts; quota giọng nói phải biểu diễn thời gian
  và usage audio riêng, không coi một phút nói là một câu chat chữ.
- `Services/Billing/IPremiumAccessService.cs` và `ISubscriptionService.cs`:
  entitlement, Admin và legacy Premium; có `ExpireForUserAsync`. Các mutation
  giọng nói cần xử lý expiry trước khi gate, không chỉ đọc role từ cookie.
- `Views/Ai/_ChatShell.cshtml` được dùng cho trang đầy đủ và embed. Đặt link tới
  trang hội thoại riêng để tránh làm lại chat chữ và tránh đòi micro trong embed.
- `wwwroot/js/speech-recorder.js` phục vụ bài nói hiện tại; không biến recorder
  theo lượt thành transport hội thoại trực tiếp. Không khôi phục Whisper local.
- `Infrastructure/SharedData/*`: mở rộng adapter có kiểu cho catalog công khai;
  custom topic, phiên, transcript và usage cá nhân luôn ở local/private DB.
- `Services/Accounts/AccountDeletionService.cs`: phải tính đến phiên voice đang
  mở và xóa transcript khi xóa tài khoản.

## 7. Quy tắc quyết định khi triển khai

1. Chọn phần nguồn đã có trước khi viết adapter, ghi rõ reuse/adapt/new.
2. Ưu tiên helper browser và mẫu server đã ghim. Chỉ viết glue C# cần thiết để
   dùng auth/DB/lifecycle có sẵn; không viết VAD, codec, STT hoặc TTS engine.
3. Nếu SDK .NET hỗ trợ đúng protocol và giảm mã/maintenance, đề xuất package
   có phiên bản chính xác cùng diff `.csproj` cho chủ dự án duyệt trước.
4. Nếu không chứng minh được lợi ích SDK, dùng adapter nhỏ với `HttpClient` và
   `ClientWebSocket` của .NET theo mẫu/API chính thức; ghi rõ lý do và kiểm thử
   protocol. Không dựng thêm Node/Python service chỉ để chạy demo nguyên bản.
5. Thêm LiveKit/Pipecat chỉ khi nhu cầu mới thực sự cần phần hạ tầng đó và chủ
   dự án chấp thuận. Không tự cài skill, MCP, CLI hoặc script mà upstream gợi ý.
6. Code nguồn mở có thể tái sử dụng; dịch vụ AI bên ngoài vẫn có thể phát sinh
   phí. Không chạy paid acceptance khi chưa có quyền và ngân sách rõ ràng.
