# Kế hoạch triển khai tính năng Premium User Writing

## 1. Mục tiêu

Cho phép người dùng gói Premium tạo một bài luyện Writing cá nhân từ bài viết bất kỳ do chính họ nhập vào.

Luồng mong muốn:
- Người dùng mở trang Writing và vào mục `Bài viết của tôi`.
- Người dùng dán một bài viết tiếng Anh hoặc tiếng Việt vào form tối giản.
- Hệ thống gọi AI để:
  - nhận diện ngôn ngữ đầu vào,
  - dịch sang ngôn ngữ còn lại,
  - tách câu thành các cặp song ngữ,
  - sinh metadata tối thiểu để bài viết có thể đi vào learner flow hiện tại.
- Hệ thống lưu thành một `WritingExercise` riêng của người dùng.
- Người dùng luyện bài viết này ngay trong learner flow Writing hiện có, không cần giao diện luyện riêng.

## 2. Ràng buộc kiến trúc hiện tại

Kế hoạch này phải bám theo codebase hiện tại:
- Learner flow Writing đang đi qua `StudyController` và `IWritingService`, không tạo logic mới trong `HomeController`.
- Domain `WritingExercise` hiện tại đang yêu cầu các trường:
  - `Title`
  - `Level`
  - `ContentType`
  - `Topic`
  - `PreviewText`
  - `IsPublished`
- Catalog Writing hiện tại đang lọc theo `Level` và `ContentType`, nên bài viết AI tạo ra vẫn phải có metadata đủ chuẩn để hiển thị đúng trong danh sách và practice flow.
- Hệ thống đã có sẵn:
  - `IAiProviderClient` / `GeminiProviderClient`
  - `IWritingService`
  - `IWritingAiEvaluationService`
  - `WritingRequestRateLimiter` cho hint/evaluate theo cửa sổ ngắn
  - `AiRequestLog` và quota AI chat theo ngày

## 3. Phạm vi tính năng

### 3.1 Trong phạm vi phase đầu

- Chỉ dành cho user có role `Premium` hoặc `Admin`.
- User chỉ cần nhập một bài viết thô vào một `textarea`.
- Hệ thống tự sinh metadata và câu song ngữ.
- Hệ thống tạo bài viết private cho đúng user sở hữu.
- UI có thêm mục `Bài viết của tôi` trong Writing.
- Owner đang là `Premium` hoặc `Admin` có thể xem, luyện và xóa bài viết của chính mình nếu bài viết đó chưa phát sinh lịch sử luyện.
- Nếu owner đã xuống `Standard`, các bài private đã tạo trước đó vẫn hiện trong `Bài viết của tôi` nhưng ở trạng thái khóa; owner không xem nội dung, không practice, không hint/evaluate và không xóa qua learner flow cho tới khi nâng cấp lại.

### 3.2 Ngoài phạm vi phase đầu

- Không cho user tự chỉnh tay từng câu sau khi AI sinh.
- Không cho user phân loại thủ công `Level`, `ContentType`, `Topic`.
- Không tạo lại một learner flow Writing riêng.
- Không mở tính năng này cho Standard user.
- Không chỉnh `Program.cs`, `appsettings.json`, hoặc `.csproj` nếu chưa có yêu cầu riêng.

## 4. Luồng người dùng

### 4.1 Tại trang Writing

Trên trang `/Home/Writing/Exercises` hoặc learner flow Writing tương ứng:
- Giữ lại các tab/bộ lọc hiện tại cho bài hệ thống.
- Bổ sung mục `Bài viết của tôi`.
- Nếu user chưa đăng nhập:
  - vẫn xem được bài public như hiện tại,
  - không thấy nút tạo bài bằng AI.
- Nếu user đăng nhập nhưng không phải Premium/Admin:
  - có thể thấy CTA bị khóa hoặc thông báo nâng cấp,
  - backend vẫn phải chặn tạo bài.
  - nếu user từng là Premium và đã có private exercise:
    - vẫn thấy danh sách bài của mình ở trạng thái locked shell,
    - chỉ thấy metadata tối thiểu như tiêu đề, preview, topic, số câu, thời gian tạo,
    - không mở được practice/detail data cho tới khi nâng cấp lại.
- Nếu user là Premium/Admin:
  - thấy nút `Tạo bài viết bằng AI`.

### 4.2 Form tạo bài

Form phase đầu nên tối giản:
- Một `textarea` cho bài viết gốc.
- Gợi ý ngắn:
  - chấp nhận tiếng Anh hoặc tiếng Việt,
  - giới hạn độ dài,
  - khuyến khích dán bài viết hoàn chỉnh.

Không bắt user nhập:
- `Level`
- `ContentType`
- `Topic`
- đáp án mẫu
- từng câu chi tiết

### 4.3 Trạng thái giao diện

Khi submit:
- disable nút submit,
- hiện spinner/skeleton,
- chặn submit lặp,
- hiển thị lỗi rõ ràng nếu AI trả lỗi hoặc vượt quota.
- backend cũng phải có guard chống duplicate-submit cho `CreateFromAi`:
  - dùng `IdempotencyKey` hoặc request fingerprint ngắn hạn,
  - hoặc bucket rate limit rất ngắn cho create endpoint,
  - để tránh tạo trùng exercise và trừ quota 2 lần khi double-click/retry/network replay.

Sau khi tạo thành công:
- redirect hoặc refresh về `Bài viết của tôi`,
- tự chọn bài mới tạo,
- cho phép bấm vào practice ngay.

## 5. Thiết kế dữ liệu

## 5.1 Cập nhật bảng `WritingExercises`

Thêm các cột sau vào `WritingExercises`:
- `UserId` `int?`
  - `NULL`: bài public do admin tạo hoặc bài hệ thống hiện có
  - có giá trị: bài private do user tạo
- `SourceType` `nvarchar(50)` hoặc tương đương
  - giá trị dự kiến:
    - `admin`
    - `premium-user-ai`

`SourceType` không bắt buộc tuyệt đối cho phase đầu, nhưng nên có vì:
- phân biệt dữ liệu hệ thống và dữ liệu user,
- dễ lọc ở UI,
- dễ audit và mở rộng sau này.

### 5.2 Quan hệ

- `WritingExercise.UserId` là khóa ngoại tới `Users.UserId`.
- Cần thêm navigation tương ứng nếu repo đang quản lý entity relationship đầy đủ.
- FK mới này bắt buộc phải cấu hình `OnDelete(DeleteBehavior.NoAction)` ở model và `ReferentialAction.NoAction` hoặc `Restrict` tương đương ở migration SQL Server.
- Không dùng cascade delete cho nhánh `User -> WritingExercise`.
- Lý do: hệ Writing hiện đã có delete path qua `WritingExercise -> WritingExerciseSentences -> UserWritingAttempts`, đồng thời user deletion path đã chạm `UserWritingAttempts`; nếu thêm cascade ở `User -> WritingExercise` sẽ rất dễ lặp lại lỗi SQL Server `multiple cascade paths` đã từng xảy ra ngày `2026-04-06`.
- Đây là ràng buộc schema bắt buộc, không phải tùy chọn tối ưu sau.
- Vì FK để `NoAction`, luồng xóa account bắt buộc phải hard-delete private Writing của user trước khi xóa dòng `Users`.

### 5.3 Index

Cần rà soát lại index vì query hiện tại đang dựa vào:
- `IsPublished`
- `Level`
- `ContentType`
- `Topic`

Sau khi có `UserId`, nên cập nhật index theo hướng hỗ trợ:
- bài public hệ thống,
- bài private theo owner,
- filter theo level/content type cho learner flow.

Ví dụ định hướng:
- index cho `UserId, IsPublished, CreatedAt`
- hoặc index kết hợp có `UserId, IsPublished, Level, ContentType`
- cần tách rõ index/query phục vụ hai nhánh:
  - catalog hệ thống public: `UserId IS NULL AND IsPublished = 1`
  - danh sách private của owner: `UserId = @currentUserId`

Chốt index cuối cùng sau khi xem query thật trong `WritingService`.

## 6. Metadata bắt buộc và cách map

Đây là điểm bắt buộc phải chốt trước khi code.

Vì learner flow hiện tại yêu cầu `Level`, `ContentType`, `Topic`, `PreviewText`, kế hoạch phase đầu sẽ dùng nguyên tắc sau:

### 6.1 User input

User chỉ nhập:
- `SourceText`

### 6.2 AI output bắt buộc

AI phải trả về JSON có tối thiểu:

```json
{
  "detectedSourceLanguage": "vi",
  "suggestedTitle": "Cuộc gọi với khách hàng",
  "suggestedTopic": "Business",
  "suggestedLevel": "intermediate",
  "suggestedContentType": "articles",
  "previewText": "Xin chào, chúng tôi muốn thảo luận về dự án mới.",
  "sentences": [
    {
      "vietnameseText": "Xin chào, chúng tôi muốn thảo luận về dự án mới.",
      "englishMeaning": "Hello, we would like to discuss the new project.",
      "breakAfter": false
    }
  ]
}
```

Lưu ý:
- `IsPublished` không thuộc contract AI output.
- AI không được quyết định visibility của exercise.

### 6.3 Quy tắc visibility do backend sở hữu

- Mọi exercise do user tự tạo từ luồng Premium/Admin trong phase này phải được backend hardcode `IsPublished = false`.
- Không lấy `IsPublished` từ AI output, client payload, hay bất kỳ default mơ hồ nào ở controller.
- Chỉ bài hệ thống/admin mới được phép đi theo nhánh public catalog: `UserId == null && IsPublished == true`.
- Điều này phải được enforce ở service tạo bài, không chỉ ở UI hay view model.

### 6.4 Fallback nếu AI trả về thiếu hoặc không hợp lệ

Không tin hoàn toàn vào output AI. Backend phải normalize và fallback:
- `suggestedTitle`
  - nếu thiếu: lấy từ 1 câu đầu đã rút gọn
- `suggestedTopic`
  - nếu thiếu: `General`
- `suggestedLevel`
  - chỉ chấp nhận các giá trị đang có trong hệ thống:
    - `beginner`
    - `intermediate`
    - `advanced`
  - nếu khác danh sách: fallback `intermediate`
- `suggestedContentType`
  - chỉ chấp nhận:
    - `emails`
    - `diaries`
    - `essays`
    - `articles`
    - `stories`
    - `reports`
  - nếu khác danh sách: fallback `articles`
- `previewText`
  - nếu thiếu: lấy từ câu đầu tiên, cắt tối đa theo giới hạn cột hiện tại

### 6.5 Quy tắc sentence output

Backend phải validate:
- có ít nhất 1 câu,
- số câu không vượt ngưỡng an toàn,
- mỗi câu có đủ:
  - `vietnameseText`
  - `englishMeaning`
- `breakAfter` là tùy chọn, nếu thiếu thì mặc định `false`

Nếu JSON AI không qua được validation:
- không lưu dữ liệu nửa chừng,
- trả lỗi thân thiện cho user,
- ghi log để debug.

## 7. Thiết kế backend

## 7.1 Controller boundary

Không nhét logic AI trực tiếp vào controller.

`StudyController` chỉ nên:
- nhận request,
- kiểm tra auth/premium entitlement,
- gọi service,
- trả JSON hoặc redirect result.

Endpoint đề xuất:
- `POST /Home/Writing/Exercises/CreateFromAi`
- hoặc giữ biến thể route tương thích với learner flow hiện tại miễn là vẫn nằm trong `StudyController`

Yêu cầu:
- `[Authorize]`
- `[ValidateAntiForgeryToken]`
- dùng `GetCurrentUserId()` từ `BaseController`

## 7.2 Service boundary

Nên thêm service mới, ví dụ:
- `IWritingGenerationService`
- `WritingGenerationService`

Trách nhiệm service này:
- validate input text,
- gọi AI provider,
- parse JSON,
- normalize metadata,
- hardcode `IsPublished = false` cho bài do user tự tạo,
- nhận `CancellationToken` từ request HTTP và truyền xuyên suốt xuống AI provider,
- enforce guard chống duplicate-submit ở phía server,
- tạo `WritingExercise`,
- tạo `WritingExerciseSentences`,
- trả về result có `ExerciseId`.

Không dồn hết vào `WritingService` hiện tại vì service đó đang nghiêng về:
- list,
- practice,
- hint,
- evaluate,
- progress tracking

## 7.3 Tái sử dụng hạ tầng AI hiện có

Không gọi Gemini trực tiếp kiểu ad-hoc nếu repo đã có abstraction.

Phải tái sử dụng:
- `IAiProviderClient`
- pattern xử lý lỗi tương tự `WritingAiEvaluationService`
- nhưng không được dùng mù quáng toàn bộ default của chatbot cho luồng create-from-AI

Luồng writing generation cần config riêng hoặc named options/named client cho tối thiểu:
- `RequestTimeoutSeconds`
- `MaxOutputTokens`
- `RequestTokenBudget`
- giới hạn input chars phù hợp với bài viết dài hơn chat thường
- cancellation propagation từ HTTP request xuống service và `IAiProviderClient`

Lợi ích:
- đồng nhất cách gọi AI,
- đồng nhất logging,
- dễ fallback,
- không tạo thêm coupling mới.

## 7.4 Transaction

Luồng tạo bài cần transaction:
- tạo `WritingExercise`
- tạo `WritingExerciseSentences`

Vì app đang dùng SQL Server retry execution strategy (`EnableRetryOnFailure()`), mọi manual transaction trong flow này bắt buộc phải được bọc trong:
- `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)`

Không được mở `BeginTransactionAsync()` trực tiếp ở controller/service rồi chạy bên ngoài execution strategy, vì pattern đó đã từng gây crash runtime ở flow Writing admin.

Mẫu bắt buộc:

```csharp
var strategy = _context.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await _context.Database.BeginTransactionAsync();
    // create WritingExercise
    // create WritingExerciseSentences
    await transaction.CommitAsync();
});
```

Nếu bước nào fail:
- rollback toàn bộ,
- không để exercise mồ côi.

## 8. Prompt AI

## 8.1 Input

Input cho AI:
- bài viết gốc user nhập

## 8.2 Nhiệm vụ của AI

AI phải:
- xác định ngôn ngữ nguồn là `vi` hay `en`,
- dịch sang ngôn ngữ đích còn lại,
- tách câu song song theo đúng thứ tự,
- sinh metadata đủ cho domain hiện tại.

## 8.3 Yêu cầu prompt

Prompt phải ép AI:
- trả về strict JSON,
- không kèm markdown,
- không thêm giải thích ngoài JSON,
- chỉ dùng enum hợp lệ cho `suggestedLevel` và `suggestedContentType`,
- giữ số lượng câu hợp lý,
- không làm lệch nghĩa giữa 2 ngôn ngữ,
- không bỏ qua câu,
- không gộp nhiều câu thành một cặp nếu không cần thiết.

## 8.4 Kiểm soát output

Backend phải parse JSON theo kiểu defensive:
- reject nếu không phải object JSON,
- reject nếu thiếu mảng `sentences`,
- reject nếu text quá dài,
- reject nếu field string vượt giới hạn DB.

## 9. Ownership và anti-IDOR

Đây là phần bắt buộc phải ghi rõ, không chỉ dừng ở thêm `UserId`.

Sau khi có private exercise, cần cập nhật ownership rule cho toàn bộ read path và mutate path liên quan.

### 9.1 Quy tắc truy cập

- Admin:
  - xem được toàn bộ
- Owner:
  - nếu đang là `Premium` hoặc `Admin`: xem/luyện/xóa được bài của chính mình theo delete policy
  - nếu đã xuống `Standard`: chỉ thấy locked shell của bài mình đã tạo trước đó, không xem nội dung và không dùng practice/hint/evaluate/delete cho tới khi nâng cấp lại
- User khác:
  - không được thấy private exercise của người khác
- Guest:
  - chỉ thấy bài public hệ thống

### 9.2 Các path phải cập nhật

Tối thiểu gồm:
- danh sách bài Writing
- data endpoint cho danh sách
- practice page
- practice data endpoint
- hint endpoint
- evaluate endpoint
- delete endpoint cho bài private

### 9.3 Quy tắc query

Với learner flow cho user đã đăng nhập:
- bài thấy được phải thỏa một trong hai điều kiện:
  - bài public hệ thống
  - bài private của chính user đó

Quy tắc tách nhánh bắt buộc:
- nhánh public hệ thống chỉ được query bằng điều kiện `UserId == null && IsPublished == true`
- nhánh private owner chỉ được query bằng điều kiện `UserId == currentUserId`
- không được để exercise user tạo đi lọt vào catalog public chỉ vì `IsPublished` bị default sai
- nếu cần gộp dữ liệu để render cùng màn hình, hãy gộp sau khi đã áp scope đúng cho từng nhánh query
- riêng với owner đã xuống `Standard`:
  - danh sách `Bài viết của tôi` vẫn có thể lấy locked shell theo `UserId == currentUserId`
  - nhưng practice page, practice data, hint, evaluate, delete endpoint vẫn phải chặn bằng entitlement hiện tại

Với user không đăng nhập:
- chỉ lấy bài public hệ thống

Với thao tác mutation trên private exercise:
- luôn query theo cả `Id` và `UserId`
- nếu không khớp thì trả `NotFound()`

## 10. Quota, rate limit và audit

Kế hoạch phase đầu tách rõ 2 loại giới hạn:

### 10.1 Burst rate limit

Giữ `WritingRequestRateLimiter` hiện tại cho:
- hint
- evaluate

Create-from-AI cũng phải có guard ngắn hạn ở server:
- bucket rate limit rất ngắn để chặn spam liên tục trong vài giây hoặc vài phút
- guard này không thay thế daily quota persisted
- guard này không thay thế `IdempotencyKey`; cần cả hai nếu muốn tránh double-submit do retry/replay

### 10.2 Daily quota cho AI generation

Không dùng riêng `WritingRequestRateLimiter` hiện tại để làm quota theo ngày vì:
- đang là in-memory,
- mất dữ liệu sau restart,
- không audit được,
- không hợp với billing/usage policy.

Phase đầu nên ưu tiên tạo bảng log/quota riêng cho writing generation thay vì tái sử dụng `AiRequestLog` trực tiếp.

Lý do:
- `AiRequestLog` hiện tại đang gắn chặt với chat conversation
- model hiện tại cần `ConversationId`
- chưa có cột phân biệt request type/source cho writing generation

Chỉ khi thật sự muốn tái sử dụng `AiRequestLog` thì plan phải ghi rõ migration mở rộng schema:
- cho phép lưu request non-chat mà không cần conversation giả
- thêm cột phân loại request/source riêng cho writing generation
- giữ audit rõ ràng giữa chat và writing generation

Yêu cầu tối thiểu:
- lưu `UserId`
- lưu thời điểm gọi
- lưu trạng thái success/fail
- lưu loại request
- có thể đếm số lần tạo bài trong ngày

Quota đề xuất phase đầu:
- Premium: 5 đến 10 bài tạo mới mỗi ngày
- Admin: không giới hạn hoặc quota riêng

Số chính xác sẽ do product quyết định trước khi triển khai.

## 11. Xóa bài và lifecycle dữ liệu

Điểm này phải chốt trước khi triển khai để tránh vướng FK và mất lịch sử luyện.

### 11.1 Phase đầu

Policy đề xuất:
- chỉ cho xóa private exercise khi chưa có `UserWritingAttempts`
- nếu đã có lịch sử luyện:
  - không xóa cứng,
  - trả thông báo cho user rằng bài đã phát sinh lịch sử luyện nên chưa thể xóa

Lý do:
- an toàn dữ liệu,
- tránh làm mất history,
- giảm rủi ro khi chưa thiết kế soft-delete đầy đủ.

### 11.2 Xóa account

Khi user tự xóa account:
- áp dụng policy `hard-delete` cho toàn bộ private Writing do user đó sở hữu trước khi xóa dòng `Users`
- bao gồm `WritingExercise`, `WritingExerciseSentences`, và mọi `UserWritingAttempts` liên quan theo cascade hiện có trong nhánh exercise/sentence
- chạy trong cùng execution strategy + transaction với luồng xóa account để tránh xóa dở dang
- đây là policy riêng cho account deletion, khác với delete từng bài trong learner flow

Lý do:
- user đã yêu cầu xóa tài khoản thì private content của họ phải bị xóa hẳn
- đồng thời cần tránh FK `NoAction` chặn việc xóa `Users`

### 11.3 Regenerate

Không overwrite bài cũ đã có attempt history.

Nếu sau này cần `Tạo lại bằng AI`:
- tạo exercise mới,
- không sửa đè exercise cũ,
- tránh làm sai lệch `UserWritingAttempts`.

### 11.4 Giai đoạn sau

Khi cần UX tốt hơn có thể nâng cấp thành:
- soft-delete với cờ `IsDeleted`
- ẩn khỏi catalog nhưng giữ history

Phase đầu chưa cần nếu muốn giữ phạm vi nhỏ.

## 12. Thiết kế giao diện

## 12.1 Mục `Bài viết của tôi`

Có thể tận dụng layout/ý tưởng từ admin Writing create, nhưng learner UI phải đơn giản hơn nhiều.

Mục này nên hiển thị:
- tiêu đề
- preview
- topic
- số câu
- thời gian tạo
- trạng thái luyện nếu có

Action:
- `Luyện ngay` nếu owner đang có entitlement hợp lệ
- `Xóa` nếu đủ điều kiện và owner đang có entitlement hợp lệ
- với owner đã xuống `Standard`:
  - item vẫn hiện trong danh sách
  - hiển thị trạng thái `Đã khóa do gói hiện tại`
  - action chính đổi thành CTA nâng cấp thay vì mở practice
  - không render nội dung chi tiết của bài

## 12.2 Nút tạo bài bằng AI

- Chỉ hiện với Premium/Admin
- Mở modal hoặc form inline
- Một `textarea`
- Một nút submit
- Có thông báo quota và giới hạn độ dài

## 12.3 Empty state

Nếu chưa có bài nào:
- hiện empty state rõ ràng
- gợi ý tạo bài đầu tiên bằng AI

## 13. Validation và giới hạn dữ liệu

Phase đầu cần chặn từ backend:
- bài nhập rỗng
- bài quá ngắn
- bài quá dài
- input chỉ toàn ký tự vô nghĩa
- AI trả về quá nhiều câu
- text dài vượt cột DB

Ngưỡng đề xuất ban đầu:
- tối đa 2000 đến 4000 ký tự cho input thô
- tối đa 80 câu sau khi AI tách

Con số chính thức cần đối chiếu thêm với token budget thực tế.

### 13.1 Timeout riêng cho AI generation

Request tạo bài bằng AI có thể nặng hơn hẳn chatbot ngắn vì phải:
- nhận input thô dài,
- dịch song ngữ,
- tách tối đa khoảng 80 câu,
- trả strict JSON hoàn chỉnh.

Vì vậy cần cấu hình timeout HTTP/cancellation budget riêng cho writing generation thay vì dùng cứng timeout mặc định của chatbot nếu timeout đó đang tối ưu cho request ngắn.

Yêu cầu plan:
- timeout phải được cấu hình ở service/options hoặc named client dành cho writing generation
- khi timeout xảy ra, trả lỗi thân thiện để user thử lại thay vì văng exception mơ hồ
- log timeout bằng mã lỗi/phân loại riêng để theo dõi chất lượng của Gemini request

### 13.2 Token budget và output budget riêng cho AI generation

Timeout riêng thôi chưa đủ cho request tạo bài dài.

Backend phải có config riêng cho writing generation để không bị cắt cụt JSON hoặc reject nhầm theo budget chat:
- `MaxOutputTokens` đủ lớn cho strict JSON nhiều câu song ngữ
- `RequestTokenBudget` riêng cho generation
- giới hạn input chars riêng nếu không muốn bị bó bởi default chat hiện tại
- parse lỗi do output bị truncate phải được coi là lỗi generation rõ ràng, không lưu dữ liệu nửa chừng

## 14. Logging và quan sát hệ thống

Cần log tối thiểu:
- user id
- request type
- success/fail
- số câu được tạo
- exercise id khi thành công
- error code khi AI fail hoặc parse fail

Không log toàn bộ bài viết raw của user ở mức info thông thường.
Nếu cần chẩn đoán sâu:
- chỉ log rút gọn,
- tránh lộ dữ liệu nhạy cảm.

## 15. Kế hoạch triển khai

### Phase 1 - Chốt domain và schema

Làm:
- thêm `UserId` cho `WritingExercise`
- cân nhắc thêm `SourceType`
- thêm quan hệ FK
- cập nhật index cần thiết
- chốt FK `User -> WritingExercise` theo `DeleteBehavior.NoAction` / `ReferentialAction.NoAction`
- tách rõ nhánh index/query cho public system và private owner
- chốt policy hard-delete private Writing trong account deletion path
- chốt bảng log/quota riêng cho writing generation nếu không mở rộng `AiRequestLog`

Deliverable:
- migration rõ ràng, không phá dữ liệu hiện có

### Phase 2 - Tạo service sinh bài bằng AI

Làm:
- tạo `IWritingGenerationService`
- gọi `IAiProviderClient`
- thiết kế prompt strict JSON
- parse và normalize output
- validate sentence payload
- ép `IsPublished = false` ở backend cho bài user tự tạo
- dùng timeout riêng phù hợp cho request generation
- dùng config riêng cho output tokens và request budget
- truyền `CancellationToken` xuyên suốt
- thêm guard chống duplicate-submit ở server

Deliverable:
- service trả về kết quả tạo bài an toàn, có fallback metadata

### Phase 3 - Mở endpoint trong `StudyController`

Làm:
- thêm endpoint create-from-AI
- check auth
- check Premium/Admin entitlement
- check daily quota
- gọi service

Deliverable:
- endpoint POST hoạt động ổn định, trả lỗi rõ ràng

### Phase 4 - Cập nhật learner flow Writing

Làm:
- thêm mục `Bài viết của tôi`
- thêm nút `Tạo bài viết bằng AI`
- thêm hiển thị bài private đúng owner
- thêm nút xóa theo policy phase đầu
- thêm trạng thái locked shell cho owner đã xuống `Standard`

Deliverable:
- owner đang là Premium/Admin nhìn thấy và luyện được bài của mình trong flow hiện tại
- owner đã xuống Standard vẫn thấy item của mình nhưng bị khóa cho tới khi nâng cấp lại

### Phase 5 - Ownership hardening

Làm:
- cập nhật toàn bộ query read path và mutate path liên quan
- bổ sung anti-IDOR cho private exercise
- giữ catalog public tách biệt hoàn toàn khỏi bài private của user

Deliverable:
- không lộ bài private sang user khác

### Phase 6 - Test và kiểm định

Làm:
- test service
- test controller
- test ownership
- test quota
- test AI malformed output
- test delete policy

Deliverable:
- đủ regression coverage để không phá flow Writing hiện tại

## 16. Test cases bắt buộc

### 16.1 Auth và entitlement

- Guest không gọi được create endpoint
- Standard user không tạo được bài
- Premium user tạo được bài
- Admin tạo được bài
- User đã xuống `Standard` vẫn thấy locked shell của bài private cũ
- User đã xuống `Standard` không mở được practice/data/hint/evaluate/delete của bài private cũ
- User nâng cấp lại thì mở lại được bài private cũ

### 16.2 Ownership

- Owner thấy bài private của mình trong `Bài viết của tôi`
- User khác không thấy bài private đó
- User khác không practice/hint/evaluate được bài private đó
- Owner xóa bài của mình thành công khi chưa có attempt
- Owner không xóa được bài đã có attempt history
- Account deletion hard-delete được toàn bộ private Writing của owner mà không vướng FK

### 16.3 AI payload

- AI trả JSON hợp lệ thì lưu được
- AI thiếu `Level` hoặc `ContentType` thì backend fallback đúng
- AI trả enum lạ thì backend normalize đúng
- AI trả JSON lỗi thì không lưu bài

### 16.4 Learner flow

- Bài user tạo xuất hiện trong catalog đúng vùng `Bài viết của tôi`
- Bài user tạo mở practice được
- Hint/evaluate vẫn hoạt động với private exercise owner sở hữu
- Bài hệ thống public không bị ảnh hưởng
- Bài private của owner đã xuống `Standard` vẫn hiện ở list nhưng không render nội dung chi tiết

### 16.5 Quota

- vượt quota ngày thì bị chặn
- restart app không làm mất số lần đã dùng trong ngày nếu dùng persisted quota

## 17. Rủi ro chính

- AI trả sentence alignment kém, làm bài luyện sai nghĩa
- metadata AI sinh không ổn định nếu không có normalization mạnh ở backend
- query ownership thiếu một path sẽ gây lộ dữ liệu private
- quota in-memory sẽ không đáng tin nếu không chuyển sang persisted logging
- xóa sai lifecycle có thể làm hỏng `UserWritingAttempts`
- quên khóa một endpoint khi owner xuống `Standard` sẽ làm entitlement bị lệch giữa list và practice
- dùng chung config AI chat có thể làm JSON generation bị truncate dù timeout đã tăng
- không có idempotency/burst guard ở server sẽ dễ tạo trùng bài và trừ quota 2 lần

## 18. Quyết định đề xuất để bắt đầu triển khai

Chốt các quyết định sau trước khi code:
- `Level` fallback: `intermediate`
- `ContentType` fallback: `articles`
- `Topic` fallback: `General`
- `PreviewText`: lấy từ câu đầu tiên đã normalize
- delete policy phase đầu:
  - chỉ xóa khi chưa có `UserWritingAttempts`
- downgrade policy phase đầu:
  - bài private cũ vẫn hiện trong `Bài viết của tôi`
  - owner đã xuống `Standard` không xem/luyện/xóa được cho tới khi nâng cấp lại
- account deletion policy:
  - hard-delete toàn bộ private Writing trước khi xóa user
- regenerate policy phase đầu:
  - tạo bài mới, không overwrite bài cũ
- quota phase đầu:
  - Premium 5 bài/ngày hoặc 10 bài/ngày
  - lưu bằng persisted log riêng cho writing generation hoặc schema mở rộng tương đương có phân loại request rõ ràng

## 19. Kết quả mong đợi của phase đầu

Sau khi hoàn thành phase đầu:
- Premium user có thể tự tạo bài Writing cá nhân rất nhanh
- bài mới đi thẳng vào learner flow hiện tại
- không phá kiến trúc `StudyController` + service layer
- không làm lộ bài private
- không làm mất lịch sử luyện viết
- có giới hạn sử dụng AI đủ an toàn để rollout nội bộ
