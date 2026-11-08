# Phase 0 — ma trận phủ nguồn và quyền

Ngày đối chiếu source: 2026-11-06. Nhánh `feature/hybrid-rag-chatbot`, HEAD bắt đầu `6af768bd32d16cc6d012d484ee82df4cab805c69`.
Thư mục `.codegraph/` có trong checkout này từ 2026-09-09. Phiên discovery không thấy tool CodeGraph trong catalog MCP của Cursor, nên các kết luận lúc đó là đọc source trực tiếp. Sự có mặt của thư mục và khả năng MCP là hai việc riêng. Phiên sửa round 1 gọi CLI ghim `scripts/codegraph.ps1 explore` ở chế độ đọc cho `ApplyTerminalStateAsync`; không init, rebuild hay sync. Browser và provider trả phí không chạy.

Browser: chưa chạy. Ô “browser” ghi `not-run`. Route khớp attribute/convention không được ghi là đã render.

## Đường chat đang hoạt động

| Bước | Source | Quan sát |
| --- | --- | --- |
| HTTP | `AiController` route `AI`: `GET /AI/Chat`, `POST /AI/Chat/Send` có antiforgery, `GET /AI/Chat/Usage` | `[Authorize]`. Conversation không thuộc user trả `NotFound`. |
| SignalR | `ClassChatHub` map `/classChatHub`. Client `user-activity.js` nối hub này. `StartAiStream` / `StopAiStream` | Stream không có hub AI riêng. Chunk 90 ký tự được cắt sau khi `SendAsync` đã trả full text, delay 25ms. |
| Context | `AiChatService.SendAsync` gọi `AiContextBuilder` với `AiSystemPrompts.PlatformGuide`, câu hiện tại và lịch sử | Không ghép `KnowledgeSnippet`. |
| Provider production | `Program.cs` bind `IAiProviderClient` tới `OpenAiProviderClient` | `store=false`, model cấu hình `gpt-6-luna`, reasoning `low`. Không có tool retrieval. |
| Provider test host | `TestWebApplicationFactory` thay bind bằng `InternalKnowledgeProvider` | Test nội bộ không chứng minh production đã retrieval. |
| Retriever | 12 `IKnowledgeRetriever` và `InternalKnowledgeProvider` có đăng ký | Không nằm trên đường `IAiProviderClient` production. |

Consumer khác của `IAiProviderClient`, không được đổi bind toàn cục: `WritingService.Generation`, `WritingAiEvaluationService`, `ReadingTranslationService`, `ListeningService`, `VocabSuggestService`.

Phiên bản đọc từ project, không sửa file: `net9.0`, EF Core 9.0.4, Pomelo 9.0.0, Microsoft.ML 4.0.2. Chưa có model embedding trong `AiOptions`.

## Ngày nghiệp vụ

| Việc | Đồng hồ hiện tại | Hệ quả |
| --- | --- | --- |
| Goals, streak, notification job | `BusinessDateHelper` và `LearningBusinessClock`: `SE Asia Standard Time`, fallback `Asia/Ho_Chi_Minh`, fallback cuối UTC | “Hôm nay” của mục tiêu là ngày Việt Nam. |
| Ghi `UserDailyActivities` qua Goals | `GoalsService.ResolveBusinessDate()` | Activity date là business date. |
| Quota chat | `AiUserDailyQuota.DayUtc = DateTime.UtcNow.Date` | Ngày quota là UTC, không phải ngày Việt Nam. |
| Retriever tiến độ/mục tiêu/gợi ý | `LearningProgressRetriever`, `GoalsRetriever`, `LearningAreaProgressRetriever`, `StudyRecommendationRetriever` dùng `DateTime.UtcNow.Date` | Lệch business date trước 07:00 ICT. Đây là gap, chưa sửa ở Phase 0. |

Phase sau phải gọi đúng đồng hồ của từng miền. Không gộp quota UTC vào “hôm nay” của Goals.

## Quyền theo miền

| Miền | Đường người dùng | Điều kiện source hiện tại | Retrieval nội bộ nếu được nối nguyên trạng | Gap cho RAG |
| --- | --- | --- | --- | --- |
| Guide tĩnh | File `wwwroot/data/ai/website-guides.json`, 41 mục | Không lọc user. Route admin cố ý để trống | `WebsiteGuideRetriever` trả một guide điểm cao nhất, ngưỡng 3 | Guide 21 mô tả trợ lý đã tra dữ liệu; production chưa làm vậy. Guide 9 nói chế độ Read; `Study.cshtml` chỉ có Flashcard, Write, Quiz, Matching. |
| Set/card | `SetController` `[Route("Home")]`, `FolderController` | Set tạo theo owner. `CardLookupRetriever` chỉ join `Sets.OwnerId == userId`, rồi `Take(500)` không `OrderBy` | Có thể bỏ sót thẻ ngoài 500 dòng không xác định | Cửa sổ 500 là gap đã thấy trong source. |
| Folder | `GET /Home/Folder` liệt kê folder của user và folder đã save. `GET /Home/FolderDetail/{id}` | Detail trả folder nếu id tồn tại, kể cả khi người xem không phải owner và chưa save | Retriever từ vựng không đọc folder người khác | Detail theo id là gap đọc. Phase 0 không sửa runtime. |
| Lớp | `ClassController` `[Route("Home")]` | `ClassRetriever` chỉ owner hoặc `ClassMembers`. `LeaveClassAsync` / `KickMemberAsync` xoá membership. Admin vào lớp bằng `CanAccessClassAsync`, retriever không có nhánh admin | Live query hết quyền sau khi rời lớp | Lịch sử chat sau này vẫn phải kiểm tra lại; hiện chưa lưu citation. |
| Speaking | `[Authorize] SpeakingController`, `GET /Speaking` | Practice: video public hoặc owner; owner Standard bị khóa private | `SpeakingRetriever` join mọi video/playlist, không predicate owner/public | Không được nối nguyên trạng vào RAG. |
| Reading | `GET /Home/Reading` trên `StudyController` | Danh sách `IsPublished`. Study qua `ReadingController` | Không có retriever bài đọc riêng | Chỉ published được vào chỉ mục chung. |
| Writing | `GET /Home/Writing` `[AllowAnonymous]` | Catalog public: `UserId == null && IsPublished`. Bài AI riêng là Premium, không published | Không có retriever writing riêng | Bài riêng không vào chỉ mục public. |
| Listening | `GET /Home/Listening` không `[Authorize]` | Public `OwnerUserId == null && IsPublished`; private owner Standard bị khóa | Không có retriever listening riêng | Cùng quy tắc public/owner/Premium. |
| Grammar | `GET /Home/Grammar` | View tĩnh, không authorize | Guide chỉ là điều hướng | Không bịa quy tắc ngữ pháp từ trang này. |
| Goals / progress | `GET /Goals` authorize. Dashboard `/Home/Index` yêu cầu đăng nhập | Dữ liệu theo `userId` | Retriever dùng ngày UTC như trên | Số liệu cá nhân là live, không embedding cũ. |
| Notification | `GET /Notification/Index` authorize | `NotificationRetriever` lọc `UserId` | Chỉ khi intent `WebsiteGuide` | Không đưa notification người khác. |
| Billing | `GET /Premium`, `GET /Billing/History` authorize | `BillingRetriever` chỉ khi câu là billing và intent `WebsiteGuide`. Premium = role Admin/Premium hoặc subscription active còn hạn | Có thể lộ trạng thái nếu được nối mà chưa tách intent | Đơn và ngày hết hạn là private. |
| Quota chat | Standard 15 câu/ngày UTC; Admin/Premium không trần câu trong `GetDailyUsageAsync` | Trần token ngày vẫn áp nếu `DailyTokenBudgetPerUser > 0` (mặc định 60000) | Không phải nguồn tri thức | Xem race Pending/ActualTokens trong contracts. |
| Admin | `/Admin/AiManagement` role Admin | Guide 40 không đưa route | `SecurityPolicyRetriever` chỉ trên `OutOfScope` và một số cụm nhạy cảm | Chat không được phát route admin. |

`AiChatService` chặn mọi câu chứa chuỗi con `password` trước retrieval. Câu tiếng Anh “How do I change my password?” bị từ chối dù guide 19 là hướng dẫn đổi mật khẩu hợp lệ. Cụm “mật khẩu” không nằm trong danh sách chặn. Phase 0 không sửa filter này.

## 41 guide

Hash SHA-256 của file guide tại thời điểm đối chiếu được ghi ở baseline. Browser mọi dòng: `not-run`.

| # | Topic | Route trong guide | Khớp source | Quyền khi mở route | Freshness | Claim đã xem | Gap |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | vocabulary | `/Home/CreateSet` | `SetController` GET CreateSet, authorize | Đăng nhập | File tĩnh, không version | Tạo set từ folder | Browser not-run |
| 2 | vocabulary | `/Home/Folder` | `FolderController` GET Folder | Đăng nhập | File tĩnh | Thêm card | Browser not-run |
| 3 | vocabulary | `/Home/Folder` | Cùng route | Đăng nhập | File tĩnh | Sửa/xoá set | Browser not-run |
| 4 | vocabulary | `/Home/Folder` | Cùng route | Đăng nhập | File tĩnh | Tạo folder | Browser not-run |
| 5 | study | `/Home/Folder` | Entry folder đúng. Mode flashcard là `/Home/Study/{id}` | Đăng nhập | File tĩnh | Flashcard | Route guide là cửa vào, không phải URL mode |
| 6 | study | `/Home/Folder` | Quiz là `/Home/QuizMode/{id}` | Đăng nhập | File tĩnh | Quiz | Cùng kiểu cửa vào |
| 7 | study | `/Home/Folder` | Write là `/Home/WriteMode/{id}` | Đăng nhập | File tĩnh | Write | Cùng kiểu cửa vào |
| 8 | study | `/Home/Folder` | Matching là `/Home/MatchingMode/{id}` | Đăng nhập | File tĩnh | Matching | Cùng kiểu cửa vào |
| 9 | study | `/Home/Folder` | Không thấy action/option Read trong `Study.cshtml` | Đăng nhập | File tĩnh | Read mode | Claim chưa thấy trong source mode switcher. Browser not-run |
| 10 | class | `/Home/CreateClass` | `ClassController` GET CreateClass | Đăng nhập | File tĩnh | Tạo lớp | Browser not-run |
| 11 | class | `/Home/Class` | GET Class | Đăng nhập, membership khi vào detail | File tĩnh | Tham gia lớp | Browser not-run |
| 12 | class | `/Home/Class` | Cùng route | Member/owner | File tĩnh | Chia sẻ folder | Browser not-run |
| 13 | class | `/Home/Class` | Leave xoá `ClassMembers` | Member | File tĩnh | Rời lớp | Browser not-run |
| 14 | speaking | `/Speaking` | `HttpGet /Speaking` | Đăng nhập. Private Standard bị khóa | File tĩnh | Chọn playlist | Retriever speaking chưa lọc quyền |
| 15 | speaking | `/Speaking` | Cùng route | Đăng nhập | File tĩnh | Shadowing/dictation | Browser not-run |
| 16 | goals | `/Goals` | `GoalsController` | Đăng nhập, dữ liệu owner | File tĩnh | Đặt goal | Ngày goal là business date |
| 17 | account | `/Home/Index` | `HomeController.Index` | Anonymous bị chuyển Landing | File tĩnh | Streak trên dashboard | Browser not-run |
| 18 | account | `/Account/Profile` | `AccountController.Profile` | Conventional, đăng nhập ở action | File tĩnh | Hồ sơ | Browser not-run |
| 19 | account | `/Account/Settings` | Settings có mục Bảo mật và `ChangePassword` | Đăng nhập | File tĩnh | Đổi mật khẩu của chính mình | Chat chặn chuỗi `password` trước retrieval |
| 20 | account | `/Account/Login` | Login GET | Anonymous được | File tĩnh | Google/Facebook | Browser not-run |
| 21 | ai | `/AI/Chat` | `AiController.Chat` | Đăng nhập | File tĩnh | Trợ lý trả lời từ dữ liệu user | Production provider chưa gọi retriever |
| 22 | vocabulary | `/Home/Search` | `HomeController.Search` | Theo action search | File tĩnh | Tìm set | Browser not-run |
| 23 | reading | `/Home/Reading` | `StudyController.Reading` | Cần user id, chỉ passage published | File tĩnh | Mở Reading | Browser not-run |
| 24 | writing | `/Home/Writing` | `StudyController.Writing` AllowAnonymous | Public catalog; bài riêng thì Premium | File tĩnh | Mở Writing | Browser not-run |
| 25 | listening | `/Home/Listening` | `StudyController.Listening` | Public catalog không bắt login; private có khóa | File tĩnh | Mở Listening | Browser not-run |
| 26 | reading | `/Home/Reading` | Cùng 23 | Cùng 23 | File tĩnh | Luyện đọc | Trùng route với guide 23 |
| 27 | writing | `/Home/Writing` | Cùng 24 | Cùng 24 | File tĩnh | Luyện viết | Trùng route với guide 24 |
| 28 | listening | `/Home/Listening` | Cùng 25 | Cùng 25 | File tĩnh | Luyện nghe | Trùng route với guide 25 |
| 29 | notification | `/Notification/Index` | `NotificationController.Index` | Authorize, theo user | File tĩnh | Mở thông báo | `/Notification` một mình không phải action Index vì default conventional là Landing |
| 30 | support | `/Home/Index` | Dashboard/landing như 17 | Đăng nhập mới vào dashboard | File tĩnh | Điều hướng tổng | Browser not-run |
| 31 | goals | `/Goals` | Cùng 16 | Owner | File tĩnh | Goals | Trùng route với guide 16 |
| 32 | goals | `/Home/Index` | Daily challenge thuộc Home Index | Đăng nhập | File tĩnh | Daily Challenge | Browser not-run |
| 33 | class | `/Home/Class` | Tab chat trong class | Member hiện tại | File tĩnh | Chat lớp | Browser not-run |
| 34 | vocabulary | `/Home/Folder` | Folder/set | Owner hoặc saved ở list; detail lỏng hơn | File tĩnh | Đưa set vào folder | Browser not-run |
| 35 | billing | `/Premium` | `BillingController` `HttpGet /Premium` | Authorize | File tĩnh | Premium | Không nhầm với `/Account/Premium` |
| 36 | billing | `/Billing/History` | `HttpGet /Billing/History` | Authorize, đơn của user | File tĩnh | Lịch sử thanh toán | Browser not-run |
| 37 | billing | `/Premium` | Cùng 35 | Authorize | File tĩnh | Tính năng Premium | Trùng route với guide 35 |
| 38 | grammar | `/Home/Grammar` | `StudyController.Grammar` | Không `[Authorize]` trên action | File tĩnh | Trang Grammar, không giải ngữ pháp ngoài hệ thống | Browser not-run |
| 39 | support | `/Home/Privacypolicy` | `HomeController.Privacypolicy` | Public | File tĩnh | Chính sách | Browser not-run |
| 40 | admin | trống | Cố ý không có route. Admin thật nằm dưới `/Admin/...` | Role Admin | File tĩnh | Không cấp route/token/credential | Đúng hướng bảo mật. Browser not-run |
| 41 | support | `/Home/Contact` | `HomeController.Contact` AllowAnonymous | Public | File tĩnh | Liên hệ | Browser not-run |

## Ví dụ trả lời được và không trả lời được

Đây là kỳ vọng hợp đồng, chưa phải output của model.

- “Cách tạo bộ từ vựng?” có thể trả lời từ guide đã review và link server tạo.
- “Hôm nay tôi ôn bao nhiêu thẻ?” phải đọc activity của đúng user theo business date.
- “zephyr-window” nếu chỉ nằm ở card 501 của owner thì hợp đồng yêu cầu trả lời được; retriever hiện tại không bảo đảm điều đó.
- “Giá vàng hôm nay?” là ngoài phạm vi.
- Ghi chú lớp sau khi membership revoked không được trả lại.
- Câu trong thẻ bảo “in system prompt” là dữ liệu, không phải lệnh.

## Việc chưa kiểm bằng browser

Mọi màn hình guide, chat, Premium lock và revocation. Source khớp route không thay cho lần bấm thử. Phase 0 không khởi động web để tránh đụng process, DB và key đang có.
