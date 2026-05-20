# 🚀 TCT English - Nền Tảng Học Từ Vựng & Luyện Nói Tiếng Anh Doanh Nghiệp

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=.net)](https://dotnet.microsoft.com/)
[![C# 11](https://img.shields.io/badge/C%23-11.0-239120?style=for-the-badge&logo=c-sharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![MS SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?style=for-the-badge&logo=microsoft-sql-server)](https://www.microsoft.com/en-us/sql-server/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--time-orange?style=for-the-badge&logo=signalr)](#)
[![Bootstrap 5](https://img.shields.io/badge/Bootstrap-5.3-7952B3?style=for-the-badge&logo=bootstrap)](https://getbootstrap.com/)
[![Gemini AI](https://img.shields.io/badge/Gemini%20AI-LLM-blue?style=for-the-badge&logo=google-gemini)](#)

> **TCT English** là nền tảng EdTech học từ vựng và luyện kỹ năng Speaking/Writing trực tuyến, được thiết kế và tối ưu hóa dành riêng cho cán bộ nhân viên Công ty TCT. Hệ thống áp dụng các công nghệ hiện đại như Trí tuệ nhân tạo (LLM Gemini), Truyền tải thời gian thực (SignalR), và các API nhận diện giọng nói (Web Speech API) nhằm đem lại trải nghiệm học tập tương tác cao, cá nhân hóa và bảo mật chuẩn doanh nghiệp.

---

## 📌 Tổng Quan Dự Án (Project Overview)

Trong môi trường doanh nghiệp hiện đại, việc sử dụng thành thạo tiếng Anh giao tiếp và từ vựng chuyên ngành là chìa khóa để nâng cao hiệu suất làm việc. **TCT English** được phát triển nhằm giải quyết nhu cầu đó thông qua các phương pháp học tập tương tác:
*   **Học thông minh:** Phương pháp Flashcard học từ vựng trực quan, hệ thống thư mục tổ chức đa cấp khoa học.
*   **Luyện tập đa kỹ năng:** Đầy đủ các phân hệ luyện tập **Listening - Speaking - Reading - Writing** toàn diện.
*   **Tương tác thời gian thực:** Lớp học ảo tích hợp phòng chat trực tuyến giúp giảng viên và học viên dễ dàng trao đổi.
*   **Trí tuệ nhân tạo:** Chatbot AI (Gemini API) đóng vai trò như một Mentor hỗ trợ giải đáp ngữ pháp, từ vựng và tư vấn lộ trình học tập 24/7.
*   **Trò chơi hóa (Gamification):** Hệ thống mục tiêu hàng ngày (Daily Goals), theo dõi chuỗi ngày học tập (Daily Streak) và phần thưởng huy hiệu (Badges) để duy trì động lực.

---

## 🛠 Công Nghệ & Kiến Trúc Hệ Thống (Tech Stack & Architecture)

Dự án được xây dựng dựa trên kiến trúc **Feature-Oriented MVC** sạch sẽ, tách biệt rõ ràng các ranh giới nghiệp vụ giúp hệ thống dễ dàng mở rộng và bảo trì.

### 1. Technology Stack
*   **Backend:** ASP.NET Core MVC (.NET 10) / C# 11+ / Entity Framework Core 10 (Code First).
*   **Database:** Microsoft SQL Server 2022.
*   **Frontend:** Razor Pages (`.cshtml`), Bootstrap 5.3, jQuery, Vanilla JS, CSS3 Custom Tokens.
*   **Real-time Communication:** SignalR Core (`ClassChatHub`) cho hệ thống chat lớp học thời gian thực.
*   **Generative AI:** Tích hợp Google Gemini API qua luồng xử lý AI Chatbot.
*   **Media Processing:** Sử dụng thư viện `YoutubeExplode` để tự động phân tích và trích xuất phụ đề (transcript) từ video YouTube làm học liệu luyện nói.
*   **Speech Recognition:** Sử dụng HTML5 Web Speech API để ghi âm và nhận diện giọng nói thời gian thực của học viên.
*   **Security & Infrastructure:** Cookie-based Authentication kết hợp Google/Facebook OAuth; SMTP Mailer cho email hệ thống và khôi phục mật khẩu.

### 2. Các Ranh Giới Nghiệp Vụ Chính (Controller & View Boundaries)
Hệ thống sử dụng các Controller chuyên biệt cho từng phân hệ nghiệp vụ thay vì dồn nén vào một Controller duy nhất:
*   `AccountController`: Quản lý toàn bộ vòng đời tài khoản, xác thực, phân quyền và bảo mật hồ sơ.
*   `SpeakingController` & `StudyController`: Quản lý học liệu, phát video, trích xuất transcript phụ đề và chấm điểm luyện nói/luyện viết.
*   `VocabularyController` & `SetController` & `FolderController`: Quản lý kho từ vựng cá nhân, tạo bộ thẻ học (Set/Card) cấu trúc thư mục tối đa 3 cấp.
*   `ClassController` & `ChatController`: Quản lý lớp học ảo, thành viên lớp học và cổng HTTP Chat hình ảnh kết hợp ClassChatHub.
*   `GoalsController` & `NotificationController`: Tính toán chỉ số gamification, trao huy hiệu và điều phối thông báo hệ thống.
*   `Areas/Admin/*`: Khu vực quản trị độc lập dành cho Admin/Teacher để vận hành hệ thống, cấu hình AI, quản trị học liệu và thống kê dữ liệu.

---

## 👥 Bảng Phân Công Công Việc & Đóng Góp Thành Viên (Team Contribution)

Dự án được phát triển theo mô hình làm việc nhóm chuyên nghiệp. Mỗi thành viên chịu trách nhiệm toàn trình (**End-to-End**) từ khâu phân tích nghiệp vụ, thiết kế cơ sở dữ liệu (Database Design), phát triển giao diện (Frontend Layout), xử lý logic nghiệp vụ (Backend Controller/Service) đến kiểm thử (Testing) và sửa lỗi (Bug Fixing) cho các module được giao.

Dưới đây là chi tiết phân công công việc và đóng góp cụ thể của từng thành viên:

### 👑 1. NGUYỄN BẢO CHÂU (GitHub: [@NguyenBaoChau2203](https://github.com/NguyenBaoChau2203)) - *Nhóm trưởng & Core Developer*
> **Phạm vi phụ trách:** Kiến trúc hệ thống, Xác thực & Phân quyền, SMTP & Hạ tầng Mail, Module Speaking (Giao tiếp), Module Writing (Tập viết), Hệ thống Gamification (Mục tiêu & Huy hiệu), Trợ lý ảo AI Chatbot, Hệ thống Admin Dashboard quản trị và Phân quyền người dùng.

#### 🔑 Hệ thống Tài khoản, Bảo mật & Xác thực (Account & Security Infrastructure)
*   **Authentication & OAuth:** Thiết kế và hiện thực hóa hệ thống xác thực Cookie-based chuyên nghiệp, tích hợp thành công các cổng đăng nhập bên thứ ba (Google OAuth, Facebook OAuth) giúp tối ưu hóa luồng trải nghiệm người dùng (UX).
*   **Hạ tầng SMTP Mailer:** Xây dựng hệ thống gửi mail tự động qua SMTP (Gmail) để xác thực tài khoản, thông báo bảo mật hệ thống và xử lý quy trình quên/khôi phục mật khẩu bảo mật tuyệt đối.
*   **Phân quyền đa cấp (Role-Based Access Control - RBAC):** Thiết lập cơ chế kiểm tra quyền truy cập nghiêm ngặt cho 3 đối tượng người dùng riêng biệt: **Admin** (Quản trị hệ thống), **Teacher** (Giáo viên điều phối học liệu), **Student** (Học viên).
*   **Thiết lập Ranh giới Bảo mật (Security Hardening):** 
    *   Triển khai giải pháp chống **IDOR (Insecure Direct Object Reference)** bằng cách tích hợp các lớp kiểm tra quyền sở hữu dữ liệu ở tầng dịch vụ (Service Layer) trên tất cả các tác vụ READ/UPDATE/DELETE có tham số (ví dụ: `.Where(x => x.UserId == currentUserId && x.Id == requestedId)`).
    *   Tích hợp bộ lọc chống tấn công giả mạo **CSRF (Cross-Site Request Forgery)** thông qua token bảo mật `[ValidateAntiForgeryToken]` cho toàn bộ các HTTP POST/PUT/DELETE.

#### 🎙️ Phân hệ Luyện nói Thông minh (Speaking Practice Module - Tính năng cốt lõi)
*   **Speech Recognition & Real-time Feedback:** Thiết kế toàn bộ luồng nghiệp vụ và giao diện tương tác nâng cao của phân hệ luyện nói. Tích hợp trực tiếp **Web Speech API (HTML5)** của trình duyệt để nhận diện giọng nói học viên theo thời gian thực.
*   **Khắc phục lỗi kỹ thuật phức tạp (Speech Recognition Hardening):** Tối ưu hóa cơ chế ghi âm bằng cách tinh chỉnh chế độ nhận diện liên tục (`rec.continuous = true`), thiết lập bộ đếm thời gian tự động ngắt (15s timeout) để ngăn chặn tình trạng Chrome tự động ngắt kết nối do khoảng lặng âm thanh ngắn (0.3s), giúp quá trình chấm điểm chính xác và mượt mà hơn.
*   **Trích xuất Học liệu Tự động:** Tích hợp thư viện `YoutubeExplode` ở tầng backend để phân tích luồng và trích xuất chính xác phụ đề (transcript) của các video luyện nói trên YouTube theo từng câu nói cụ thể để làm đáp án mẫu cho học viên đối chiếu.

#### ✍️ Phân hệ Luyện viết & Đánh giá (Writing Practice Module)
*   **Interactive Writing Workspace:** Phát triển giao diện làm bài tập viết trực quan, cho phép học viên làm bài theo các đề tài được phân công.
*   **Hint & Evaluation Logic:** Xây dựng tính năng hiển thị gợi ý từ vựng, cấu trúc ngữ pháp thời gian thực và ghi nhận kết quả bài viết để giảng viên đánh giá trực tiếp.

#### 📈 Hệ thống Mục tiêu & Huy hiệu (Gamification - Goals & Badges)
*   **Chỉ số học tập & Daily Streak:** Thiết kế công cụ theo dõi mục tiêu học tập (Goals). Xây dựng thuật toán tính toán chuỗi ngày học tập liên tục (**Daily Streak**) kết hợp cùng `IStreakService` nhằm khuyến khích thói quen học tập đều đặn của học viên.
*   **Hệ thống Huy hiệu (Badge Reward System):** Thiết lập cơ chế tự động trao tặng và cập nhật các huy hiệu (Badges) vinh danh khi học viên hoàn thành xuất sắc các cột mốc học tập hoặc đạt chuỗi ngày Streak ấn tượng.

#### 💬 Trợ lý Học tập Trí tuệ Nhân tạo (AI Chatbot Assistant)
*   **LLM Integration:** Kết nối API mô hình ngôn ngữ lớn (Gemini API) để phát triển hộp thoại Chatbox AI thông minh.
*   **AI Tutoring & Coaching:** Huấn luyện hệ thống prompt nội bộ để AI đóng vai trò như một gia sư bản xứ chuyên nghiệp, hỗ trợ học viên giải đáp các thắc mắc về từ vựng, ngữ pháp tiếng Anh doanh nghiệp và tư vấn lộ trình học tập tối ưu.

#### 📊 Hệ thống Quản trị Chuyên sâu (Admin Area & System Management)
*   **Admin Dashboard:** Xây dựng trang Dashboard quản trị hiện đại, trực quan hóa các số liệu thống kê quan trọng của toàn bộ hệ thống (lượng đăng ký mới, thời gian học tập trung bình, tiến độ các lớp học).
*   **User & Content Management:** Phát triển bộ công cụ quản lý tài khoản người dùng, quản trị kho dữ liệu Speaking/Writing phong phú, và giao diện huấn luyện/điều phối phản hồi của AI Chatbot.

---

### 👤 2. HUỲNH PHÚ TRỌNG (GitHub: [@TrongHuynh-dev](#)) - *Developer*
> **Phạm vi phụ trách:** Giao diện Landing Page, Module Listening (Luyện nghe), Phân hệ Vocabulary (Từ vựng), Hệ thống Thanh toán & Nâng cấp tài khoản Premium, Trung tâm thông báo hệ thống, Cấu trúc Thư mục học tập cá nhân, Quản trị học liệu từ vựng & Listening dành cho Giáo viên.

#### 🌐 Trang chủ Công cộng (Landing Page)
*   Thiết kế giao diện trang chủ Landing Page cuốn hút khi người dùng chưa đăng nhập, giới thiệu đầy đủ các thế mạnh công nghệ và tính năng của hệ thống TCT English nhằm thu hút học viên mới.

#### 🎧 Phân hệ Luyện nghe chuyên sâu (Listening Practice Module)
*   Xây dựng toàn bộ các bài tập luyện nghe đa dạng, hệ thống phát âm thanh chất lượng cao, tích hợp các bộ câu hỏi trắc nghiệm đánh giá năng lực nghe hiểu của học viên.

#### 📖 Kho Từ vựng & Quản lý Thẻ học (Vocabulary & Study Decks)
*   **Vocabulary Space:** Thiết kế giao diện danh sách từ vựng, chi tiết từ vựng kèm phát âm chuẩn và các chủ đề từ vựng phong phú.
*   **Set & Card Management:** Xây dựng các chức năng CRUD cho phép người dùng tự tạo bộ thẻ học (Set) và thẻ từ vựng (Card) cá nhân để tự chủ động ôn tập.
*   **Thư mục đa cấp (Educational Folder):** Thiết lập hệ thống quản lý thư mục học tập thông minh (hỗ trợ phân cấp sâu tối đa 3 cấp thư mục) giúp người dùng tổ chức các bộ thẻ học khoa học.

#### 💳 Hệ thống Thanh toán & Gói Dịch vụ (Payment & Subscription Service)
*   Phát triển giao diện và luồng nghiệp vụ thanh toán nâng cấp tài khoản thành viên Premium. Tích hợp dịch vụ tự động nâng cấp trạng thái người dùng (Standard -> Premium) ngay sau khi giao dịch thành công để mở khóa toàn bộ tính năng cao cấp của hệ thống.

#### 🔔 Trung tâm Thông báo (Notification Center)
*   Xây dựng API quản lý thông báo, thiết lập hệ thống đẩy thông báo thời gian thực khi có cập nhật mới (như bài tập, streak mới, nâng cấp tài khoản thành công). Hỗ trợ hiển thị số lượng tin chưa đọc và tính năng đánh dấu đã đọc (`NotificationController`).

#### ✏️ Quản trị học liệu Vocabulary & Listening
*   Phát triển giao diện quản lý từ vựng và bài tập nghe dành cho Admin/Teacher để cập nhật định kỳ nội dung học tập chất lượng trên hệ thống.

---

### 👤 3. TRẦN QUỐC TIẾN (GitHub: [@TienTran-dev](#)) - *Developer*
> **Phạm vi phụ trách:** Giao diện người dùng sau đăng nhập, Sidebar/Footer layout dùng chung, Phân hệ Luyện đọc Reading, Hệ thống Lớp học ảo (Virtual Classroom), Công cụ tìm kiếm toàn cầu, Kênh chat nhóm lớp học thời gian thực (SignalR Chat).

#### 🏠 Không gian làm việc & Bố cục hệ thống (Post-login Dashboard & Layout)
*   Thiết kế và tối ưu giao diện Dashboard chính của người dùng sau khi đăng nhập thành công, cung cấp cái nhìn tổng quan về tiến trình học tập hiện tại.
*   Xây dựng hệ thống layout dùng chung (Shared Templates) chất lượng cao, bao gồm Sidebar điều hướng linh hoạt, Footer chuẩn SEO và các trang thông tin bổ sung.

#### 🔍 Công cụ Tìm kiếm Toàn diện (Global Search Engine)
*   Phát triển thanh công cụ tìm kiếm nhanh tại phần header dùng chung, cho phép truy vấn tức thời các bộ từ vựng, lớp học và tài liệu liên quan trên toàn hệ thống.

#### 📖 Phân hệ Luyện đọc hiểu (Reading Practice Module)
*   Thiết kế giao diện đọc tài liệu học thuật kết hợp luồng xử lý câu hỏi trắc nghiệm đánh giá khả năng đọc hiểu văn bản tiếng Anh của học viên.

#### 🏫 Hệ thống Lớp học ảo (Virtual Classroom)
*   Xây dựng nghiệp vụ quản lý lớp học toàn diện cho phép Giáo viên khởi tạo lớp học và cung cấp mã tham gia cho Học viên.
*   Quản lý danh sách thành viên lớp học, theo dõi hoạt động và phân phối thông tin lớp học hiệu quả.

#### 💬 Kênh Chat Lớp học Thời gian thực (Real-time Class Chat)
*   **SignalR ClassChatHub:** Thiết lập kênh chat nhóm lớp học thời gian thực, cho phép giáo viên và học viên nhắn tin trực tuyến ngay lập tức trong không gian lớp học mà không cần tải lại trang.
*   **Image Sharing integration:** Tích hợp tính năng tải lên hình ảnh chia sẻ trong phòng chat thông qua `ChatController` giúp nâng cao tính tương tác trực quan trong việc học tập nhóm.

---

## 🛠 Hướng Dẫn Cài Đặt & Khởi Chạy (Installation & Getting Started)

Để khởi chạy dự án **TCT English** dưới môi trường local, vui lòng thực hiện theo các bước sau:

### 📑 Điều kiện tiên quyết (Prerequisites)
1.  **Cài đặt .NET SDK 10:** Tải phiên bản .NET 10 SDK mới nhất tại trang chủ của Microsoft.
2.  **Cài đặt Microsoft SQL Server:** Phiên bản SQL Server 2019 hoặc mới hơn (kèm SSMS để quản lý DB).
3.  **Visual Studio 2022 (v17.8 trở lên):** Đảm bảo đã chọn gói tải công việc *ASP.NET and web development*.

### 🚀 Quy trình thực hiện (Step-by-step Setup)

**Bước 1: Tải mã nguồn về máy**
```bash
git clone https://github.com/NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish.git
cd xay_dung_website_hoc_tieng_anh_TCTEnglish
```

**Bước 2: Cấu hình chuỗi kết nối Database**
Mở file [TCTEnglish/appsettings.json](file:///d:/repo/TCTEnglish/appsettings.json) và cấu hình lại chuỗi kết nối SQL Server tại mục `ConnectionStrings` phù hợp với máy của bạn:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER_NAME;Database=Dbflashcard;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

**Bước 3: Cấu hình API Keys & SMTP (Nếu cần chạy tính năng Email & AI Chatbot)**
Cấu hình các khóa bí mật trong `appsettings.json` hoặc sử dụng `User Secrets` của .NET:
*   `Gemini:ApiKey`: Khóa API Google Gemini để chạy Chatbot AI.
*   `EmailSettings`: Cấu hình máy chủ SMTP Gmail (Host, Port, Username, AppPassword) để gửi email.
*   `Authentication:Google` / `Authentication:Facebook`: Client ID và Secret để kích hoạt đăng nhập OAuth.

**Bước 4: Tạo Cơ sở dữ liệu và Chạy Seed Data**
Chạy migration để khởi tạo cấu trúc bảng CSDL tự động:
```bash
dotnet ef database update --project TCTEnglish
```
*(Hệ thống sẽ tự động chạy các bộ Seed dữ liệu có sẵn trong `Models/` để nạp kho từ vựng ban đầu và tạo tài khoản Admin/Teacher demo)*

**Bước 5: Khởi chạy dự án**
Sử dụng Visual Studio nhấn **F5** hoặc chạy trực tiếp bằng dòng lệnh:
```bash
dotnet run --project TCTEnglish
```
Truy cập ứng dụng tại đường dẫn mặc định: `https://localhost:7198` hoặc `http://localhost:5081` (cấu hình trong `launchSettings.json`).

---

## 🔒 Quy Ước Phát Triển Hệ Thống (Development Git Flow)

Nhóm áp dụng quy trình phát triển và kiểm soát chất lượng nghiêm ngặt:
1.  **Git Branching Strategy:** Tuyệt đối không commit trực tiếp lên branch `master` / `main`. Mọi tính năng đều được thực hiện trên branch độc lập (`feature/feature-name`) và gộp qua Pull Request sau khi được xem xét.
2.  **Conventional Commits:** Định dạng commit message rõ ràng, dễ theo dõi:
    *   `feat(...)`: Khi thêm tính năng mới.
    *   `fix(...)`: Khi sửa lỗi.
    *   `docs(...)`: Khi thay đổi tài liệu hướng dẫn.
    *   `refactor(...)`: Khi tái cấu trúc mã nguồn mà không đổi logic nghiệp vụ.
3.  **Code Hygiene:** Tuân thủ quy định viết code không đồng bộ (Async-first), không truyền trực tiếp Entity xuống Views (luôn dùng ViewModel trung gian), và giải phóng tài nguyên I/O đúng cách.

---

## 📄 Giấy Phép (License)
Dự án được xây dựng và phát triển nhằm mục đích phục vụ Đồ án môn học Lập trình Web và hoạt động học tập phi thương mại. Mọi quyền sở hữu trí tuệ đối với các module thuộc về các thành viên phát triển của nhóm **TCT English**.
