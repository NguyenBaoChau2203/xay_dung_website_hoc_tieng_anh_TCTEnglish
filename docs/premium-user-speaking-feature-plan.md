# Kế hoạch chuyển Premium YouTube import sang Speaking

## 1. Mục tiêu

Chuyển hướng feature đang nghĩ cho `Listening` sang đúng domain `Speaking`, để user có thể thêm video YouTube riêng vào khu luyện nói.

Mục tiêu product cuối:

- feature nằm ngay trên trang `Speaking`,
- có section `Bài nói của tôi`,
- user nhập đúng 1 field là `YouTube URL`,
- hệ thống tự lấy metadata và transcript,
- practice bằng chính flow Speaking hiện có,
- `Standard` vẫn thấy giao diện import nhưng khi dùng sẽ hiện yêu cầu nâng cấp Premium,
- chỉ hỗ trợ video tiếng Anh và transcript/captions tiếng Anh,
- transcript policy là `YouTube captions first + Gemini fallback`,
- không translation,
- không dùng Whisper cho flow user-import của Speaking.

## 2. Kết luận kiến trúc

### 2.1 Feature này phải nằm trong boundary `Speaking`

Feature mới không nên tiếp tục đi qua `StudyController`, `Views/Study/Listening`, hay domain `ListeningExercise`.

Nó nên sống trong đúng boundary:

- `SpeakingController`
- `Views/Speaking/`
- `Speaking*ViewModels`
- speaking service mới hoặc speaking service slice mới

### 2.2 Với Speaking, nên ưu tiên tái sử dụng domain hiện có

Speaking hiện đã có:

- `SpeakingVideo`
- `SpeakingSentence`
- `UserSpeakingProgress`
- admin create flow cho speaking videos
- learner practice flow cho pronunciation / dictation

Vì vậy hướng khuyến nghị là:

- mở rộng domain Speaking hiện có để hỗ trợ `public/admin` và `private/user-owned`,
- không xây thêm một domain học mới song song chỉ để rồi lại phải map về Speaking practice.

### 2.3 Không dùng `ListeningExercise*` làm sản phẩm cuối

Worktree hiện đang có một phần scaffold Listening dở dang. Với hướng mới, đống đó chỉ nên được xem là:

- nguồn tham khảo để tái sử dụng helper, import policy, test idea,
- không phải product path cuối.

Kết luận:

- giữ lại các phần dùng chung có giá trị,
- port logic cần thiết sang Speaking,
- không tiếp tục đầu tư thêm vào `ListeningExercise`, `ListeningExerciseSentence`, `IListeningService`, `ListeningService`, `Views/Study/Listening*` cho feature này.

## 3. Rule nghiệp vụ đã chốt

### 3.1 Entitlement

- `Premium` và `Admin` có thể import video riêng vào `Speaking`.
- `Standard` vẫn nhìn thấy giao diện import.
- Khi `Standard` bấm dùng, hệ thống phải hiện thông báo yêu cầu nâng cấp lên `Premium`.
- `Standard` không được tạo mới, không được mở practice cho private imported videos, và không được xóa item private khi đang locked.

### 3.2 English-only

Phase này không cho user chọn `Ngôn ngữ video` hay `Phụ đề`.

Thay vào đó, hệ thống mặc định áp dụng rule:

- chỉ nhận video tiếng Anh,
- chỉ nhận transcript/captions tiếng Anh usable,
- nếu không xác nhận được transcript usable bằng tiếng Anh thì từ chối import với thông báo thân thiện.

### 3.3 Transcript policy

- ưu tiên caption/transcript có sẵn từ YouTube,
- nếu caption thiếu hoặc unusable thì fallback sang Gemini để tạo transcript,
- transcript cuối cùng phải usable bằng tiếng Anh,
- không dịch transcript,
- không thêm AI grading, translation, hay chat UX mới trong phase này.

## 4. Phạm vi phase 1

### 4.1 Trong phạm vi

- `Premium` và `Admin` import được video YouTube riêng vào `Speaking`.
- `Standard` vẫn thấy đầy đủ UI import.
- Form chỉ có `YouTube URL`.
- Trang `Speaking` có section `Bài nói của tôi`.
- Item private của user dùng lại flow luyện nói hiện có:
  - pronunciation / shadowing,
  - dictation,
  - progress tracking.
- Hệ thống enforce `English-only`.
- User downgrade xuống `Standard`:
  - vẫn thấy danh sách item cũ dưới dạng locked shell,
  - không tạo mới,
  - không mở practice,
  - không xóa khi đang locked.

### 4.2 Ngoài phạm vi

- Không cho user chọn `Level`, `Topic`, `Playlist`.
- Không cho user chọn `Ngôn ngữ video`.
- Không cho user chọn `Phụ đề`.
- Không share video private của user cho user khác.
- Không thêm translation.
- Không thêm upload file local.
- Không thêm flow mới ở `Listening`.

## 5. Hướng UX

## 5.1 Vị trí giao diện

Giao diện import phải được thêm trực tiếp vào trang `Speaking`, không tạo màn hình import riêng.

## 5.2 Visual direction

AI agent có thể được yêu cầu bám theo tinh thần của mockup user gửi:

- một hero import panel rộng nằm ở phần trên của trang `Speaking`,
- nền gradient xanh,
- copy ngắn, rõ, mang tính CTA,
- một chip/tab `Youtube`,
- một ô nhập link lớn,
- một nút CTA nổi bật ở bên phải.

Nhưng phase này phải rút gọn mockup như sau:

- không có dropdown `Ngôn ngữ video`,
- không có dropdown `Phụ đề`,
- không có local upload thực tế,
- không có flow chọn nhiều nguồn import.

## 5.3 Hành vi theo role

### `Premium` / `Admin`

- thấy giao diện import,
- nhập URL,
- submit để tạo video riêng trong `Bài nói của tôi`.

### `Standard`

- vẫn thấy y nguyên giao diện import,
- vẫn bấm CTA được,
- nhưng submit sẽ trả về upgrade prompt rõ ràng:
  - inline error,
  - toast,
  - hoặc modal upgrade.

Không ẩn giao diện import khỏi `Standard`.

## 5.4 `Bài nói của tôi`

Section này nên hiển thị:

- thumbnail,
- title,
- preview ngắn,
- duration,
- sentence count,
- ngày tạo,
- badge `Riêng tư`,
- trạng thái `Sẵn sàng`, `Đang xử lý`, hoặc `Đã khóa`.

Action:

- `Luyện ngay`
- `Xóa`

Khi user downgrade:

- action chính đổi thành `Nâng cấp để mở khóa`,
- metadata cơ bản vẫn hiển thị,
- không mở practice.

## 6. Thiết kế dữ liệu khuyến nghị

### 6.1 Hướng khuyến nghị: mở rộng `SpeakingVideo`

Thay vì tạo một bảng học mới, nên mở rộng `SpeakingVideo` để hỗ trợ private owner-scoped content.

Các field nên bổ sung:

- `OwnerUserId` (`int?`)
- `SourceUrl` (`nvarchar(500)?`)
- `SourceType` (`nvarchar(50)`)
- `TranscriptSource` (`nvarchar(50)?`)
- `ImportStatus` (`nvarchar(50)`)
- `CreatedAt` (`datetime2`)

Field hiện có nên được cho phép nullable cho private rows nếu cần:

- `PlaylistId`
- `Level`
- `Topic`

Rule dữ liệu:

- public/admin videos:
  - `OwnerUserId = null`
  - vẫn có `PlaylistId`, `Level`, `Topic`
- private user-imported videos:
  - `OwnerUserId = currentUserId`
  - `SourceType = premium-user-youtube`
  - không bắt user phân loại

### 6.2 `SpeakingSentence`

Tiếp tục dùng `SpeakingSentence` để tránh phải nhân đôi practice + progress infrastructure.

Với private imported videos:

- `Text` đến từ transcript sentence,
- `VietnameseMeaning` không được generate trong phase này.

Khuyến nghị an toàn cho phase đầu:

- lưu `VietnameseMeaning = ""` cho private imports,
- practice UI ẩn hoặc bỏ qua translation UI khi chuỗi rỗng.

### 6.3 Index/FK đề xuất

- `IX_SpeakingVideos_OwnerUserId_CreatedAt`
- unique guard cho `(OwnerUserId, YoutubeId)` với private rows
- `IX_SpeakingSentences_VideoId_StartTime`

FK:

- `SpeakingVideo.OwnerUserId -> Users.UserId`
- dùng `DeleteBehavior.NoAction`

## 7. Service và controller boundary

`SpeakingController` hiện đang controller-heavy. Feature này nên tranh thủ tách sang service mới thay vì làm controller dày thêm.

Khuyến nghị:

- tạo `ISpeakingService` / `SpeakingService`,
- hoặc tối thiểu một speaking service slice cho:
  - public catalog load,
  - private import,
  - practice access,
  - delete,
  - progress access check.

Endpoints đề xuất:

- `GET /Speaking`
- `POST /Speaking/My/Create`
- `POST /Speaking/My/Delete`
- `GET /Speaking/Practice/{id}`
- giữ `POST /api/speaking/{sentenceId}/progress`, nhưng phải harden ownership/public access.

Admin create và learner private import nên dùng chung parse + transcript plumbing, nhưng khác business rules:

- admin create: public catalog, có level/topic/playlist
- user import: private owner-scoped, URL-only UX, không classification

## 8. Quy tắc security

- Tất cả read/mutate private content phải theo pattern owner filter và trả `NotFound()` nếu không khớp.
- `SaveSpeakingProgress` hiện mới check sentence tồn tại; khi có private imports thì endpoint này phải verify sentence đó thuộc video public hoặc thuộc private video mà current user được phép access.
- Duplicate rule: một user không được import cùng một `YoutubeId` nhiều lần vào `Bài nói của tôi`.
- English-only rule: chỉ cho import khi transcript/captions usable là tiếng Anh.

## 9. Kế hoạch di dời từ worktree Listening hiện tại

### 9.1 Nên giữ / tái sử dụng

- `TCTEnglish/Services/YoutubeUrlHelper.cs`
- caption-first + Gemini fallback logic trong `TCTEnglish/Services/YoutubeTranscriptService.cs`
- `YoutubeTranscriptModels.cs`
- ý tưởng test về URL normalization và transcript acquisition

### 9.2 Nên port sang Speaking

- entitlement check của private import
- duplicate-per-owner logic
- import transaction pattern
- card/list/locked-shell view-model ideas

### 9.3 Không nên tiếp tục như product path cuối

- `ListeningExercise.cs`
- `ListeningExerciseSentence.cs`
- `IListeningService.cs`
- `ListeningService.cs`
- `Views/Study/Listening.cshtml`
- `Views/Study/ListeningPractice.cshtml`
- migration `AddListeningExercises`
- test suite đang naming theo Listening

## 10. Phase triển khai

- `Phase 0`: pivot audit, chốt keep / port / drop từ scaffold Listening.
- `Phase 1`: refactor schema sang Speaking.
- `Phase 2`: service extraction + import backend.
- `Phase 3`: UI integration ở `Views/Speaking`.
- `Phase 4`: practice + progress hardening.
- `Phase 5`: account cleanup + regression suite.
- `Phase 6`: independent review + fix.
- `Phase 7`: final closure review.

## 11. Test checklist

- Guest không tạo được.
- `Standard` thấy được giao diện import.
- `Standard` bấm submit nhận thông báo nâng cấp Premium.
- Premium tạo được.
- Admin tạo được.
- Owner downgrade thấy locked shell và không practice được.
- User khác không thấy / không delete / không ghi progress cho private item của owner khác.
- watch URL, youtu.be, shorts URL hoạt động.
- caption-first path hoạt động.
- Gemini fallback path hoạt động.
- video không phải tiếng Anh bị chặn đúng cách.
- duplicate theo owner bị chặn.
- private imported video mở đúng practice page.
- translation UI không vỡ khi `VietnameseMeaning` rỗng.
- xóa account dọn private speaking imports liên quan.

## 12. Kết luận

Nếu đi theo hướng này thì feature sẽ:

- đúng với nhu cầu thật là `Speaking`, không phải `Listening`,
- tái sử dụng được admin add-video flow và practice/progress hiện có,
- tránh phải tạo thêm một domain học song song rồi lại nối về Speaking,
- tận dụng được phần transcript import đã làm dở ở nhánh Listening,
- và phù hợp hơn với boundary hiện có của repo sau refactor.
