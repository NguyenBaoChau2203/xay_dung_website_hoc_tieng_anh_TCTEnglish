# AI Chatbot Integration Plan

Tài liệu này mô tả kiến trúc AI chat đang dùng trong repo hiện tại sau vòng
hardening follow-up. Đây là bản cập nhật theo code thực tế, không còn giả định
Razor Pages.

## 1. Kiến trúc hiện tại

- App model: ASP.NET Core MVC trên `.NET 10`
- HTTP entrypoint:
  - `TCTEnglish/Controllers/AiController.cs`
- View:
  - `TCTEnglish/Views/Ai/Chat.cshtml`
- Frontend script:
  - `TCTEnglish/wwwroot/js/ai-chat.js`
- Service layer:
  - `TCTEnglish/Services/AI/*`
- Realtime entrypoint:
  - `TCTEnglish/Hubs/ClassChatHub.cs`
- Realtime payloads:
  - `TCTEnglish/Realtime/AiStreamMessages.cs`

Nguyên tắc boundary:

- `AiController` chỉ orchestration HTTP, không tự `new` service/provider
- `ClassChatHub` chỉ auth, route event realtime, lifecycle stream
- Business logic nằm trong `Services/AI`
- User identity đi qua `BaseController.GetCurrentUserId()` hoặc
  `CurrentUserIdExtensions`, không parse claim trực tiếp

## 2. Data model

AI chat hiện dùng 3 entity chính:

- `AiConversation`
- `AiMessage`
- `AiRequestLog`

Mapping nằm trong `TCTEnglish/Models/DbflashcardContext.cs`.

Mục đích:

- `AiConversation`: nhóm lịch sử chat theo user
- `AiMessage`: lưu message user/assistant/system theo conversation
- `AiRequestLog`: lưu observability cho từng request AI

## 3. Service graph

### HTTP flow

`AiController` inject:

- `IAiConversationService`
- `IAiChatService`
- `IAiObservabilityService`
- `IAiRequestRateLimiter`

### Chat flow

`IAiChatService` hiện được triển khai bởi `AiChatService`.

`AiChatService` phụ thuộc vào:

- `DbflashcardContext`
- `IAiContextBuilder`
- `IAiProviderClient`
- `IAiConversationExecutionGuard`
- `IOptions<AiOptions>`
- `ILogger<AiChatService>`

### Realtime flow

`ClassChatHub` inject:

- `IClassService`
- `IAiStreamingService`
- `DbflashcardContext`
- `ILogger<ClassChatHub>`

`IAiStreamingService` hiện được triển khai bởi `AiStreamingService`.
Service này chịu trách nhiệm:

- rate limit cho luồng stream
- giữ active stream theo connection
- chống stream song song trên cùng conversation
- gọi `IAiChatService`
- chuẩn bị chunk replay cho client

## 4. Provider + DI

`Program.cs` hiện bind `AiOptions` từ:

- `AI:*` nếu có section riêng
- fallback `OpenAiApiKey` cho config cũ

Provider resolution hiện đi qua DI và `AiOptions.Provider`:

- `OpenAI` hoặc rỗng -> `OpenAiProviderClient`
- `Azure OpenAI` -> chưa implement, hiện fail fast rõ ràng

Điều này giúp “đổi provider bằng config” là đúng ở mức wiring, không còn
hardcode `new OpenAiProviderClient(...)` trong controller/hub.

## 5. HTTP chat flow

Luồng `POST /AI/Chat/Send`:

1. `AiController.Send` kiểm tra auth + antiforgery + model binding
2. `IAiRequestRateLimiter` chặn burst request
3. `IAiChatService.SendAsync(...)` xử lý nghiệp vụ
4. `AiChatService` xác thực ownership conversation
5. `AiContextBuilder` chọn history theo budget
6. `IAiProviderClient` gọi provider
7. assistant message + request log được persist
8. response trả về `ChatReplyDto`

Security guard hiện có:

- `[Authorize]`
- `[ValidateAntiForgeryToken]`
- anti-IDOR trên conversation ownership
- block prompt injection pattern cơ bản
- per-user/per-IP request rate limiting
- concurrent guard trên cùng conversation

## 6. Realtime flow

Luồng `ClassChatHub.StartAiStream(...)`:

1. xác thực user hiện tại
2. gọi `IAiStreamingService.StartStreamAsync(...)`
3. service tạo stream session và gọi `IAiChatService`
4. hub phát:
   - `AiStreamStarted`
   - `AiStreamChunk`
   - `AiStreamCompleted`
   - hoặc `AiStreamFailed`
5. `StopAiStream` hủy theo `connectionId + streamId`

Lưu ý quan trọng:

- Realtime hiện là simulated streaming
- Provider vẫn trả full response trước
- Hub sau đó replay theo chunk qua SignalR

Không nên claim đây là token streaming end-to-end.

## 7. Observability + governance

Hiện đã có:

- request log theo `AiRequestLog`
- snapshot observability cho user hiện tại
- planned token budgeting qua `AiContextBuilder`
- actual token logging từ provider reply
- daily token budget theo user

Chưa hoàn tất:

- tokenizer thật thay cho heuristic
- business timezone cho daily budget
- distributed rate limit
- retention policy cho conversation/log

## 8. Test baseline

AI baseline hiện được cover bởi:

- `TCTEnglish.Tests/AiPhase1DataAndContextTests.cs`
- `TCTEnglish.Tests/AiPhase2ServiceTests.cs`
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`

Các nhóm chính:

- context builder
- conversation ownership
- `AiChatService` happy path / error path / concurrency
- auth / anti-IDOR / provider failure integration

## 9. Những việc còn mở

Các việc chưa xem là done production-ready:

- dùng tokenizer thật
- chuyển rate limiting khỏi in-memory local state
- tách business date/timezone cho budget theo ngày
- retention/anonymization cho `AiRequestLog`
- real streaming nếu product thực sự cần time-to-first-token thấp hơn

## 10. File map nhanh

- Controller: `TCTEnglish/Controllers/AiController.cs`
- Hub: `TCTEnglish/Hubs/ClassChatHub.cs`
- Services: `TCTEnglish/Services/AI/`
- Models: `TCTEnglish/Models/AiConversation.cs`,
  `TCTEnglish/Models/AiMessage.cs`,
  `TCTEnglish/Models/AiRequestLog.cs`
- ViewModels: `TCTEnglish/ViewModels/AI/`
- View: `TCTEnglish/Views/Ai/Chat.cshtml`
- JS: `TCTEnglish/wwwroot/js/ai-chat.js`
