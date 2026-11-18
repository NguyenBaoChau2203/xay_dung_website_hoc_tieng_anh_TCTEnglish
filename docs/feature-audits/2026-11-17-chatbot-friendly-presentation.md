# Friendly chatbot presentation — verified repair ledger

Updated: 2026-11-17. Branch: `fix/writing-goals-profile-polish`.
Repair scope: T1 and R1-R4 from the
[independent review](2026-11-17-chatbot-friendly-review.md), following the
[repair plan](../superpowers/plans/2026-11-17-chatbot-friendly-review-repair.md).
One GPT agent performed investigation, implementation, review and verification.
All pre-existing uncommitted work, including generated email previews, was preserved.

## Current result and correction of the initial report

- **R1:** Explicit `unverified` projections now show one friendly caution even
  without Notice/sources, in Razor history and the common HTTP/stream/older-message
  renderer. Metadata-absent legacy history remains distinct and gets no inferred
  caution. Changed/snapshot notices are preserved. Unavailable/revoked bodies stay
  replaced by the backend, with no duplicate footer or navigation actions.
- **R3:** Client anchors use `setAttribute('data-source-key', source.sourceKey)`.
  Keys match reloaded Razor history and remain invisible. Freshness/outcome gates,
  route allowlists, deduplication, three-action limit and `_top` remain enforced.
- **R4:** Fixed allowlisted Font Awesome 6 classes replace both new SVG maps.
  Decorative spans remain `aria-hidden=true`, with visible action labels. Browser
  testing found the standalone embedded document lacked this stylesheet; it now
  loads the exact 6.0.0 URL already used by `_Layout.cshtml`. No new family,
  version, CDN origin, package, shared layout or design token was introduced.
- **R2:** The authored voice pass is complete as an inventory, with changes and
  deliberate retentions distinguished below. The initial report overstated the
  number of rewritten branches and contained examples absent from source. Those
  examples are historical claims, not evidence of implemented behavior. Every
  example in the current table comes from actual source or a named test/browser
  observation. Literal learning material, numbers, roles, routes and access rules
  are preserved. Copy no longer promises a button that might not render.
- **T1:** The old Antigravity inline `node -e` command failed before policy
  evaluation through Python/cmd.exe. Its four hooks now run the existing local
  file as `node scripts/agent-tools.cjs hook antigravity <Event>` from the open
  workspace root. cmd.exe and pwsh pass in a relocated checkout containing spaces.
  Native Antigravity loading/transport is still unobserved; this is local launch
  evidence, not a claim of successful native delegation.

The initial submission is retained at the end as explicitly superseded historical
evidence. The independent review's demonstrated failures have not been erased.

## Authored reply inventory and actual examples

Disposition is relative to the repair baseline, which already contained some
friendly wording. Dynamic values in braces below describe actual source templates;
concrete examples are attributed to tests or the browser.

| Reply family / branch | Disposition | Actual wording or preserved behavior |
| --- | --- | --- |
| Greeting | Already appropriate | `Chào bạn! 👋 Mình là trợ lý TCT English. Hôm nay mình cùng luyện kỹ năng nào nhỉ?` |
| Vocabulary with sets | Changed | `Mình tìm thấy 2 bộ từ vựng của bạn 📚` — TemplateAnswerComposerTests; set names and 18/12 card counts retained. |
| Vocabulary without sets | Already appropriate | `Hiện tại mình chưa tìm thấy bộ từ vựng nào trong tài khoản của bạn 🌱` |
| Card found | Changed | `Mình tìm thấy từ **forecast** rồi nè 📖`, followed by `Trong bộ từ **Business Terms**:`. Definition, phonetic and example remain stored material. |
| Card missing | Changed | `Mình chưa tìm thấy từ "{safeTerm}" trong các bộ từ vựng của bạn 🔎` — no invented definition. |
| Card multiple / ambiguity | Already appropriate | Existing choices preserve exact set names/definitions; `Bạn muốn nghĩa ở bộ nào?` remains a clarification. |
| General starter recommendation | Existing warm body; closing corrected | `Mình gợi ý vài lựa chọn nhẹ nhàng để khởi đầu hôm nay nhé 🌱`; explicitly described as `gợi ý khởi đầu chung`, with no promise of nonexistent buttons. Observed in real HTTP response. |
| Personal recommendation | Changed | `Hôm nay mình cùng ôn lại những phần này nhé 🌱`; the test's Daily English, 7 remaining cards, 3-day streak and 4-card goal are preserved. |
| Recommendation with no eligible data/starters | Changed | `Mình chưa có đủ dữ liệu để gợi ý riêng cho bạn 🌱`; factual lack of personal evidence stays explicit. |
| Speaking with results | Changed | `Mình gợi ý vài bài luyện nói để bạn chọn nhé 🎙️`; actual titles, levels, topics and entitlement counts are preserved. |
| Speaking without results | Already appropriate; entitlement serious | `Mình chưa tìm thấy bài speaking phù hợp trong hệ thống lúc này 🎙️`; no invented lesson. Existing Premium gate/fallback is unchanged. |
| Class with membership | Changed | `Mình tìm thấy 2 lớp học bạn đang tham gia:` — test retains owner/member roles, class names and member counts. |
| Class without membership | Changed | `Mình chưa thấy lớp học nào bạn đang tham gia 🌱`; create/join guidance remains factual. |
| Vocabulary progress with/without data | Already appropriate | Existing `Tiến độ học của bạn:` metrics and no-data guidance stay accurate; no emoji added to each metric. |
| Learning-area progress | Intentionally factual | Reading/Writing/Listening/Speaking totals, availability and Dashboard/Courses guidance are unchanged. |
| Goals and badges | Closing changed; metrics factual | `Bạn có thể mở trang **Goals** để xem mục tiêu và danh hiệu 🎯`; XP, targets, awarded dates and eligibility are unchanged. |
| Navigation guide found | Closing changed | `Bạn có thể mở mục này trên website để bắt đầu 🌱`; actual authored body remains intact. Placeholder/missing routes still get no route/CTA promise. |
| Navigation guide missing | Already appropriate | `Mình chưa tìm thấy hướng dẫn phù hợp với câu hỏi của bạn 🌱` |
| Notifications | Closing changed; facts retained | `Bạn có thể mở mục **Thông báo** để xem chi tiết.` Unread counts and message titles remain factual. |
| Billing / Premium | Intentionally serious; closing corrected | Existing payment-detail redaction remains. `Bạn có thể xem gói tại **Premium** và đơn của mình tại **Lịch sử thanh toán**.` No prices, entitlement or payment state invented. |
| Out of scope | Already appropriate | Platform-only support and examples remain; no unrelated factual answer invented. |
| Sensitive data / injection refusal | Intentionally serious | Existing calm refusal and extraction/access protections remain unchanged. |
| Quota, provider failure and cancellation | Intentionally serious | AiChatService, AiProviderErrorMessages and existing retry/unknown-outcome behavior are retained. No claim of an unused charge/refund, successful provider response or automatic retry. |
| Realtime initialization fallback | Changed presentation only | `Mình chưa kết nối được chế độ trả lời trực tiếp. Bạn vẫn có thể gửi câu hỏi tại đây.` HTTP fallback behavior is unchanged. |
| Unverified retained history | Changed | `Mình chưa kiểm tra lại được thông tin của câu trả lời này. Bạn hỏi lại mình nhé.` Exactly one caution; no CTA. |

The FAQ file is `_version: 2.1.0`, with **66 raw guide entries**, **64 entries with
question examples** and **279 examples**. The earlier figure 64 referred to the
question-bearing bank, not the raw entry count. FAQ bodies/IDs/routes/aliases
were reviewed and retained in this repair: procedural text is already suitable,
and safety/availability/entitlement instructions deliberately stay factual.
The three starter bodies were already warm at the repair baseline. No shared
lesson content, retrieval data contract or stored chat history was rewritten.

### FAQ body disposition by current entry

| Guide ID | Repair disposition | Current body opening (verbatim) |
| --- | --- | --- |
| `create-set` | Retained: procedural guidance | Để tạo bộ từ, mở Bộ từ vựng và chọn Tạo bộ từ mới. Nhập tên, lưu bộ rồi thêm các thẻ cần học. Chỉ chỉnh sửa những bộ mà tài khoản của bạn có quyền quản lý. |
| `add-card` | Retained: procedural guidance | Mở Bộ từ vựng, chọn bộ bạn được sửa rồi dùng chức năng thêm thẻ. Nhập từ, nghĩa và các thông tin bổ sung có trên biểu mẫu; lưu xong kiểm tra lại danh sách thẻ. |
| `edit-delete-set` | Retained: procedural guidance | Mở Bộ từ vựng và chọn bộ thuộc quyền quản lý của bạn để đổi tên, sửa thẻ hoặc xóa bộ. Kiểm tra kỹ hộp xác nhận trước khi xóa; trợ lý chỉ hướng dẫn, không thực hiện xóa thay bạn. |
| `create-folder` | Retained: procedural guidance | Thư mục giúp sắp xếp các bộ từ vựng theo nhóm (tối đa 3 cấp lồng nhau). |
| `study-flashcard` | Retained: procedural guidance | Mở khu vực Luyện tập, chọn bộ từ được phép xem và chế độ Flashcard để lật thẻ, xem nghĩa rồi tự ôn. Chỉ số luyện tập trên trình duyệt và tiến độ ôn tập trên tài khoản có thể khác nhau; làm theo tùy chọn ghi nhận ôn tập của trang. |
| `study-quiz` | Retained: procedural guidance | Mở khu vực Luyện tập, chọn bộ từ và chế độ Quiz để làm câu hỏi trắc nghiệm. Số câu phụ thuộc dữ liệu đủ điều kiện của bộ. Kết quả một lượt chơi không tự bảo đảm mọi từ đã đạt thành thạo. |
| `study-write` | Retained: procedural guidance | Write/Typing là luyện gõ từ vựng theo gợi ý, khác với bài luyện viết câu trong Writing. Mở Luyện tập, chọn bộ từ và chế độ gõ từ phù hợp; muốn luyện viết câu thì mở Writing. |
| `study-matching` | Retained: procedural guidance | Mở Luyện tập, chọn bộ từ và chế độ Match để ghép từ với nghĩa. Làm theo cách chọn cặp đang hiển thị; số cặp tùy lượt chơi. Kết quả ghép cặp không đồng nghĩa tự động hoàn thành mục tiêu hay nhận danh hiệu. |
| `study-read` | Retained: procedural guidance | Muốn luyện đọc hiểu, hãy mở Reading và chọn một bài đang được phép xem. Không có một chế độ Read riêng được xác nhận trong danh sách tám chế độ của trang Luyện tập từ vựng. |
| `create-class` | Retained: procedural guidance | Để tạo lớp học: |
| `join-class` | Retained: procedural guidance | Để tham gia lớp học: |
| `share-folder-to-class` | Retained: procedural guidance | Chủ lớp có thể chia sẻ thư mục từ vựng vào lớp để thành viên cùng học: |
| `leave-class` | Retained: procedural guidance | Để rời lớp học: |
| `speaking-playlist` | Retained: procedural guidance | Trang Speaking có video luyện nói theo cấp độ và chủ đề. Dùng bộ lọc để tìm bài, mở video có transcript rồi luyện từng câu. Có thể chọn bài công khai hoặc bài cá nhân mà tài khoản được phép dùng. |
| `speaking-practice` | Retained: procedural guidance | Để luyện Shadowing, mở Speaking, chọn video có transcript, nghe một câu rồi nói theo. Dùng phần ghi âm và chấm điểm khi trang báo khả dụng; kết quả chỉ xuất hiện sau lượt xử lý thực tế, không phải chỉ vì đã phát video. |
| `set-daily-goal` | Retained: procedural guidance | Bạn có thể đặt mục tiêu học hằng ngày tại trang **Goals**: |
| `view-streak` | Retained: procedural guidance | Chuỗi ngày học được theo dõi từ hoạt động học hợp lệ, không chỉ từ việc đăng nhập hay mở trang. Xem trạng thái chuỗi tại Goals hoặc hồ sơ. Nếu chuỗi chưa cập nhật, kiểm tra hoạt động đã được ghi nhận và điều kiện hiển thị trước khi báo lỗi. |
| `account-profile` | Retained: procedural guidance | Vào **Hồ sơ cá nhân** để: |
| `change-password` | Retained: deliberately factual/safety guidance | Để đổi mật khẩu, mở Cài đặt tài khoản, tìm phần Bảo mật và làm theo bước xác minh. Nếu quên mật khẩu, dùng chức năng Quên mật khẩu trên trang đăng nhập. Không gửi mật khẩu hoặc mã xác minh vào chatbot. |
| `login-google-facebook` | Retained: procedural guidance | Bạn có thể đăng nhập TCT English bằng tài khoản Google hoặc Facebook: |
| `ai-chatbox-usage` | Retained: procedural guidance | Chatbox AI Trợ lý của TCT English hỗ trợ các câu hỏi liên quan đến: |
| `search-set` | Retained: procedural guidance | Bạn có thể tìm kiếm bộ từ vựng trên TCT English: |
| `reading-quick-action` | Retained: procedural guidance | Reading nằm trong nhóm Courses. Bạn mở trang Reading để chọn bài đọc, đọc nội dung và làm câu hỏi kiểm tra đọc hiểu. |
| `writing-quick-action` | Retained: procedural guidance | Writing cho phép chọn cấp độ và bài tập để luyện viết câu. Trang danh sách có bài công khai, bài cá nhân được phép xem và bài luyện gần đây. Chức năng tạo bài và phản hồi AI tùy quyền truy cập, hạn mức và trạng thái dịch vụ đang hiển thị. |
| `listening-quick-action` | Retained: procedural guidance | Mở Listening để tìm bài nghe theo các bộ lọc hiện có, rồi chọn bài muốn học. Transcript, câu hỏi và từ vựng phụ thuộc dữ liệu của từng bài; không phải mọi video đều mặc định có đủ các phần này. |
| `reading-practice` | Retained: procedural guidance | Mở Reading, chọn bài đọc đang được phép xem rồi đọc nội dung và làm các câu hỏi có trong bài. Nếu cần tìm một bài cụ thể, dùng tên bài hoặc chủ đề. Không cần có bộ từ vựng cá nhân mới được bắt đầu đọc bài công khai. |
| `writing-practice` | Retained: procedural guidance | Mở danh sách bài tập Writing, chọn cấp độ, dạng bài và chủ đề rồi mở bài muốn luyện. Đọc câu gợi ý, viết câu trả lời và gửi theo hướng dẫn của trang. Không cần tạo một bộ từ vựng riêng để chọn bài Writing công khai. |
| `listening-practice` | Retained: procedural guidance | Mở Listening, chọn bài nghe rồi dùng các điều khiển và nội dung hỗ trợ thực tế của bài. Nếu có transcript hoặc câu hỏi, dùng chúng để kiểm tra phần đã nghe; nếu chưa có, không nên coi đó là dữ liệu đã hoàn thiện. |
| `notifications` | Retained: procedural guidance | Bạn có thể xem thông báo bằng biểu tượng quả chuông ở góc phải màn hình. Nếu muốn xem danh sách đầy đủ, mở trang Thông báo trong thanh điều hướng bên trái. |
| `site-overview-navigation` | Retained: procedural guidance | Trang Dashboard là điểm bắt đầu sau khi đăng nhập. Thanh điều hướng bên trái cho phép mở Courses, Goals, Thông báo, Vocabulary, Classes và Contact. |
| `goals-overview` | Retained: procedural guidance | Trang Goals giúp bạn đặt mục tiêu học theo ngày và xem tiến độ theo từng khu vực học. |
| `daily-challenge` | Retained: procedural guidance | Daily Challenge nằm trên Dashboard sau khi đăng nhập. Mở thử thách và chọn đáp án theo hướng dẫn. Hoạt động và phần thưởng được backend kiểm tra; việc chỉ mở Dashboard không được coi là đã hoàn thành thử thách. |
| `class-chat` | Retained: procedural guidance | Trong trang chi tiết lớp, tab Chat cho phép thành viên trao đổi tin nhắn và gửi ảnh học tập. |
| `organize-sets-in-folders` | Retained: procedural guidance | Thư mục giúp gom các bộ từ theo chủ đề. Khi tạo hoặc chỉnh sửa bộ từ, bạn chọn thư mục cần lưu; sau đó mở **Thư mục** để xem lại các set đã được sắp xếp. Nếu cần chia sẻ cho lớp, hãy mở chi tiết lớp và dùng chức năng chia sẻ thư mục. |
| `premium-pricing` | Retained: procedural guidance | Trang Premium hiển thị các gói đang mở bán, thời hạn sử dụng và phương thức thanh toán được hỗ trợ. Để nâng cấp: mở trang **Premium**, chọn gói phù hợp, chọn cổng thanh toán như VNPay hoặc MoMo nếu đang khả dụng, rồi hoàn tất trên cổng thanh toán. Không chia sẻ API key, mã giao dịch gateway, link thanh toán riêng hoặc thông tin cấu hình trong chat. |
| `payment-history` | Retained: procedural guidance | Bạn có thể xem các đơn thanh toán của chính mình tại trang **Lịch sử thanh toán**. Trang này hiển thị gói đã chọn, số tiền, nhà cung cấp thanh toán và trạng thái xử lý. Nếu đơn đang chờ hoặc cần kiểm tra thủ công, hãy liên hệ hỗ trợ thay vì gửi thông tin bí mật qua chat. |
| `premium-feature-access` | Retained: procedural guidance | Quyền sử dụng chức năng nâng cao phụ thuộc gói và hạn mức tài khoản. Mở Premium để xem quyền lợi đang hiển thị; khi tạo bài Writing hoặc nhập video luyện nói, làm theo trạng thái truy cập của trang. Không suy đoán giá, hạn mức hay quyền mua từ câu trả lời tĩnh. |
| `grammar-page` | Retained: procedural guidance | Trang Grammar nằm trong nhóm Courses và dành cho nội dung ngữ pháp trên nền tảng. Chatbox chỉ hướng dẫn cách mở và sử dụng trang Grammar; nếu bạn yêu cầu giải bài ngữ pháp ngoài hệ thống, chatbox sẽ từ chối lịch sự. |
| `privacy-terms` | Retained: deliberately factual/safety guidance | Bạn có thể xem thông tin chính sách quyền riêng tư và điều khoản sử dụng trên các trang công khai của TCT English. Chatbox không cung cấp hoặc suy đoán thông tin bí mật như connection string, OAuth secret, SMTP credential, token, cookie hoặc dữ liệu của người dùng khác. |
| `admin-management-overview` | Retained: deliberately factual/safety guidance | Khu vực Admin chỉ dành cho tài khoản có vai trò Admin. Các nhóm quản lý chính gồm dashboard, người dùng, thông báo, AI, billing/premium, vocabulary set, Reading, Listening, Writing và Speaking. Vì lý do bảo mật, chatbox không cung cấp route nội bộ, cách vượt quyền, token, credential hay logic backend; hãy đăng nhập đúng vai trò và dùng menu Admin trong giao diện. |
| `contact-support` | Retained: deliberately factual/safety guidance | Mở trang Liên hệ, điền họ tên, email nhận phản hồi, chủ đề và nội dung rồi gửi biểu mẫu. Nếu tài khoản bị khóa, chọn chủ đề khiếu nại tài khoản; trang này dùng được khi chưa đăng nhập. Không gửi mật khẩu hoặc mã xác minh. Thời gian phản hồi không được bảo đảm bởi chatbot. |
| `starter-vocabulary` | Retained: existing warm starter | Cùng mở Luyện tập, chọn một bộ từ bạn thích rồi ôn một lượt ngắn nhẹ nhàng với Flashcard hoặc Quiz nhé 🌱. |
| `starter-speaking` | Retained: existing warm starter | Mình cùng thử một câu ngắn nhé 🎙️ Bạn chọn video có transcript ở Speaking rồi nghe và nói theo nha. |
| `starter-writing` | Retained: existing warm starter | Có nè ✍️ Bạn mở Writing, chọn cấp Cơ bản và một dạng bài muốn luyện rồi thử viết từng câu nhé. |
| `speaking-youtube` | Retained: procedural guidance | Mở Speaking, tìm khu vực bài cá nhân hoặc nhập video YouTube và dán liên kết hợp lệ. Việc nhập phụ thuộc quyền tài khoản, hạn mức, khả năng lấy nội dung và transcript. Theo dõi trạng thái xử lý; không coi dán link là đã có bài luyện hoàn chỉnh. |
| `speaking-recent` | Retained: procedural guidance | Mở Speaking và xem khu vực video luyện nói gần đây khi đã đăng nhập. Danh sách phản ánh các bài bạn đã mở học và còn được phép xem; một video bị xóa hoặc khóa có thể không còn mở được. |
| `speaking-transcript` | Retained: procedural guidance | Trong bài Speaking, dùng transcript đã có để chọn câu và theo dõi lời nói. Nếu bài chưa có transcript hoặc chưa xử lý xong, kiểm tra trạng thái nhập bài và các lựa chọn bổ sung phụ đề đang hiển thị. Chatbot không tự tạo lời thoại hoặc mốc thời gian để lấp chỗ thiếu. |
| `speaking-microphone` | Retained: procedural guidance | Mở bài Speaking, chọn câu và dùng nút ghi âm. Cho phép quyền micro trên trình duyệt, kiểm tra thiết bị đầu vào rồi thử lại nếu chưa thu được tiếng. Chỉ bấm gửi khi sẵn sàng; chatbot không tự bật micro hoặc gửi bản ghi thay bạn. |
| `speaking-scoring` | Retained: deliberately factual/safety guidance | Trong bài Speaking, chọn câu, ghi âm và gửi chấm khi chức năng khả dụng. Xem điểm và phản hồi trả về từ lượt chấm thực tế. Nếu dịch vụ báo lỗi hoặc chưa có kết quả, không suy đoán điểm; giữ thông tin lỗi cần thiết để thử lại có chủ đích hoặc báo hỗ trợ. |
| `writing-recent` | Retained: procedural guidance | Mở danh sách bài tập Writing và xem khu vực bài luyện viết gần đây sau khi đăng nhập. Chọn bài để quay lại luyện. Bài đã xóa hoặc không còn quyền truy cập sẽ không được mở chỉ nhờ có lịch sử. |
| `writing-level-format` | Retained: procedural guidance | Danh sách Writing có các cấp Cơ bản, Trung cấp và Nâng cao. Dùng bộ lọc để chọn Email, Nhật ký, Bài luận, Bài viết, Câu chuyện hoặc Báo cáo theo lựa chọn hiện có. Không đồng nhất ba cấp Writing với nhãn A1–C2 của video Speaking. |
| `writing-ai-brief` | Retained: procedural guidance | Trong Bài viết của tôi, chọn chế độ mô tả bài muốn luyện. Nhập yêu cầu, chọn cấp độ, dạng bài và các lựa chọn đang có rồi gửi tạo bài nếu tài khoản được phép. Chế độ này sáng tác bài từ yêu cầu, khác với dán một bài hoàn chỉnh để chuyển thành bài tập. |
| `writing-ai-paste` | Retained: procedural guidance | Trong khu vực tạo bài cá nhân, chọn Dán bài có sẵn, nhập văn bản tiếng Việt hoặc tiếng Anh và làm theo giới hạn đang hiển thị. Hệ thống chuyển nội dung thành bài tập song ngữ khi tạo thành công. Không dán thông tin bí mật hoặc dữ liệu cá nhân không cần thiết. |
| `writing-ai-fallback` | Retained: deliberately factual/safety guidance | Nếu OpenAI không tạo được bài và có khung phù hợp, giao diện có thể đề nghị khung song ngữ nội bộ. Xem giới hạn, điền các thông tin được hỗ trợ và xác nhận trước khi tạo. Khung có phạm vi chủ đề giới hạn, không dịch tùy ý mọi bài dán và không bảo đảm giữ các chi tiết ngoài khung. |
| `writing-feedback` | Retained: procedural guidance | Mở bài Writing, đọc gợi ý rồi nhập và gửi câu trả lời. Xem đánh giá và nội dung sửa từ kết quả thực tế. Phản hồi AI phụ thuộc quyền, hạn mức và dịch vụ; không phải chỉ mở bài hoặc tạo bài đã được chấm điểm hay tính hoàn thành. |
| `writing-personal` | Retained: procedural guidance | Các bài do bạn tạo xuất hiện trong Bài viết của tôi và được giữ riêng theo quyền truy cập. Mở bài để luyện hoặc dùng thao tác quản lý đang hiển thị. Xóa bài cần xác nhận; trợ lý không xóa hoặc công khai bài cá nhân thay bạn. |
| `practice-hub` | Retained: procedural guidance | Trang Luyện tập cho phép chọn bộ từ và chế độ như Learn, Test, Match, Flashcard, Quiz, Typing, Listening hoặc Cloze. Các chế độ này dùng dữ liệu bộ từ, khác với các trang Reading, Writing, Listening và Speaking theo bài học. |
| `practice-hard-words` | Retained: procedural guidance | Mở Từ khó trong khu vực Luyện tập để xem các từ được hệ thống lựa chọn từ dữ liệu của bạn. Nếu danh sách trống, bắt đầu một lượt học và kiểm tra lại; không thể suy ra điểm yếu cụ thể khi chưa có dữ liệu phù hợp. |
| `goals-badges` | Retained: procedural guidance | Mở Goals để xem danh hiệu, điều kiện và trạng thái đã nhận. Danh hiệu phụ thuộc hoạt động hợp lệ và điều kiện riêng, không chỉ số lần đăng nhập. Nếu chưa được nhận, kiểm tra tiến độ và điều kiện trong thẻ danh hiệu; chatbot không tự cấp danh hiệu hoặc đoán bạn đã mở khóa. |
| `profile-badges` | Retained: procedural guidance | Mở hồ sơ cá nhân để xem các danh hiệu đã được ghi nhận cho tài khoản. Muốn tìm điều kiện hoặc danh hiệu chưa mở khóa, chuyển sang Goals. Chatbot không dùng dữ liệu của tài khoản khác để quyết định bạn đã nhận danh hiệu nào. |
| `account-avatar-name` | Retained: procedural guidance | Mở hồ sơ cá nhân và dùng các biểu mẫu đổi tên hoặc ảnh đại diện. Với ảnh, làm theo định dạng và giới hạn tải lên đang hiển thị. Lưu xong kiểm tra kết quả; trợ lý không thay đổi tài khoản chỉ từ một tin nhắn. |
| `account-email` | Retained: deliberately factual/safety guidance | Mở Cài đặt tài khoản và phần email để thực hiện đổi hoặc xác minh địa chỉ theo các bước bảo mật được yêu cầu. Kiểm tra hộp thư và thư rác khi chờ email xác minh. Không gửi mã xác minh, đường dẫn bảo mật hoặc mật khẩu vào chatbot. |
| `account-delete` | Retained: deliberately factual/safety guidance | Mở Cài đặt tài khoản và tìm khu vực xóa tài khoản. Đọc kỹ cảnh báo và hoàn thành bước xác minh/xác nhận của hệ thống. Một số tài khoản hoặc quan hệ dữ liệu có thể cần xử lý trước; chatbot không xóa tài khoản hay bỏ qua kiểm tra an toàn thay bạn. |
| `notification-preferences` | Retained: procedural guidance | Mở Cài đặt tài khoản, phần thông báo để xem tùy chọn nhắc học và email định kỳ. Push còn cần quyền trình duyệt và thiết bị đăng ký hợp lệ; email cần địa chỉ đủ điều kiện và cấu hình gửi hoạt động. Bật một ô chọn chưa chứng minh thư hoặc thông báo đã đến thiết bị. |
| `streak-troubleshooting` | Retained: procedural guidance | Nếu đăng nhập hoặc học xong chưa thấy chuỗi, mở Goals để kiểm tra ngày học và tiến độ hoạt động đã ghi nhận. Đăng nhập, mở bài, tạo bài hoặc phát video không tự bảo đảm đã có một lượt học hợp lệ. Nếu đã hoàn thành đúng luồng mà số liệu vẫn sai, gửi thông tin bài/thời điểm cho hỗ trợ, tránh gửi thông tin đăng nhập. |
| `assistant-scope` | Retained: procedural guidance | Bạn có thể hỏi cách dùng TCT English, mở một khu vực học, gợi ý khởi đầu và dữ liệu học mà tài khoản được phép xem. Trợ lý dẫn tới các nguồn có căn cứ; nó không biết mọi câu hỏi bên ngoài hệ thống, không tự cấp điểm/quyền và không cung cấp bí mật hay dữ liệu người khác. |

## Verification: observed local results

| Check | Result |
| --- | --- |
| 10-class focused .NET filter, including new regressions | **240 passed, 0 failed, 0 skipped** |
| Four focused chat Node suites | **26 passed, 0 failed** |
| Node setup/hook policy suite | **11 passed, 0 failed** |
| Full Python scripts suite | **153 total: 150 passed, 3 skipped** |
| Tooling validator | PASS |
| Configured hook command smoke checker | PASS: all four hosts through pwsh, Antigravity additionally through cmd.exe |
| Windows relocated-checkout regression | PASS: actual checked command, workspace path with spaces, allowed/wrong/missing/malformed model metadata, malformed JSON and no private input echo |
| Release application rebuild after embedded stylesheet repair | **0 errors, 42 existing warnings**; the earlier incremental build was 0 errors/0 warnings |
| Scoped integrated diff / whitespace | Reviewed; `git diff --check` passes |

Red evidence recorded before repair:

- Windows hook regression: five cmd.exe model subcases failed with the exact
  `[eval]:1` / `"const` / `Unterminated string constant` symptom; pwsh worked.
- Node: 5 failed / 8 passed before client repair (three missing identity paths,
  HTTP and stream unverified cautions). Final source-key checks include a hostile
  title on an allowed route; no injected image is created.
- Razor unverified history: corrupt/future metadata failed (2), metadata-absent
  legacy passed (1). Final full filter passes all three.
- Icon mapping: 9 failed before replacing SVG output with fixed existing-family
  classes. Real iframe then exposed its missing stylesheet; the added embedded
  integration assertion failed once before that dependency was repaired.
- Copy/false-button cases: 6 failed / 1 passed before the voice repair. Five old
  presentation expectations in the first integrated run were reconciled without
  deleting access, source, ownership or XSS assertions.

Exact commands (no ellipsis filters):

```powershell
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
node --test scripts/tests/test_agent_setup.cjs
uv run --no-project python scripts/check_team_hooks.py
dotnet build TCTEnglish/TCTEnglish.csproj --configuration Release
node --test scripts/tests/test_ai_chat_phase6_sources.cjs scripts/tests/test_ai_chat_clay_ui_and_logo.cjs scripts/tests/test_ai_chat_load_older_order.cjs scripts/tests/test_ai_chat_submit_guard.cjs
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --filter 'FullyQualifiedName~ChatLearningAssistantTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~AiRagBoundaryAccessTests|FullyQualifiedName~AiRagPhase2FixRound1Tests|FullyQualifiedName~AiRagPhase2FixRound2Tests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~AiProductionClassifierIntegrationTests|FullyQualifiedName~AiRagPhase6UiResilienceTests' --logger 'console;verbosity=minimal' -- xUnit.MaxParallelThreads=2
```

Python skips: one non-Windows-only rejection check on this Windows host, and two
CTC cases requiring the separate pinned alignment-only Docker runtime. These are
not declared passed. Logs are ignored under `.appdata/local/chat-friendly-repair-*`.

## Browser evidence and limits

Real Chrome, synthetic local Admin account, launcher on port 5102. Saved keys and
credentials were not printed or rewritten. Text-provider BaseUrl was process-only
loopback `http://127.0.0.1:9/v1/`; process-only whitespace SMTP password prevented
mail transport. Speech status was configured/valid metadata, `VerifiedLive=false`;
no audio, paid AI request or SMTP send was initiated.

- New starter question `Gợi ý cho tôi học gì hôm nay?`: observed one HTTP send,
  explicit general starter prose and three safe `_top` actions with keys
  `guide:starter-vocabulary`, `guide:starter-speaking`, `guide:starter-writing`.
- Follow-up `Mình muốn luyện viết tiếng Anh`: observed real AiStreamStarted and
  AiStreamCompleted/current, no second HTTP send; the accepted Writing action
  retained `guide:writing-practice`. Reload preserved all four action identities.
- Full chat at actual 1440/768 px and emulated **390 px**: document width matched
  viewport, CTAs stayed inside the viewport. Screenshots at 1440 and 390 were
  visually inspected. The initial resize request for 390 was clamped to 500 by
  Chrome; the final 390 check used device emulation and measured innerWidth=390.
- Tab and Shift+Tab moved between the starter actions in the expected order.
- Safe API fixtures exercised production `loadOlderMessages` in the real DOM:
  unverified body + one caution/no CTA; legacy body/no inferred warning;
  changed/snapshot notices retained; unavailable replacement body/no duplicate;
  current allowed route + hostile title/source key remained text/attribute data,
  with no injected image. Fixtures were transient network/DOM inputs, not stored
  conversation-history edits. Automated tests also cover HTTP/stream cautions.
- A safe HTTP503 fixture showed the existing unknown-outcome guidance, retained
  the draft and entered error state after one intercepted send, without paid
  transport or automatic retry.
- Actual Dashboard floating iframe: width/document width **418 px**, no routine
  source/state panels. After repair, computed pseudo-element font was Font Awesome
  6 Free with visible glyphs, fixed classes and hidden decorative semantics.
  Writing CTA navigated the **top page** to `/Home/Writing/Exercises`.
- Native browser zoom remains unverified: Control++ returned without changing
  innerWidth=1440 or DPR=1; emulation is not claimed as native 200% zoom.

Native Antigravity transport/trust/loading and successful native subagent
execution remain unobserved. The live application is packaged; no native worker
was invoked to probe it. Reload the intended Antigravity session with the selected
model and confirm its hooks run from the open workspace root. No trust bypass,
metadata fabrication, model substitution or Codex multi-agent enablement occurred.
Standard account boundaries are automated-test evidence, not this Admin browser
session. No CI, staging or production verification/deployment is claimed.

Final environment check: after the successful browser acceptance above, a later
launcher restart could not bring Docker Desktop up (`context deadline exceeded`).
`docker info` then reported the dockerDesktopLinuxEngine named pipe absent, and
`docker desktop status` could not retrieve a running status. The final browser
reload consequently returned connection refused; port 5102 is not left running.
This is a current local-environment blocker to another live run, not an additional
passed check. No database reset, Docker/WSL shutdown or unrelated process restart
was attempted to recover it. The observed earlier browser results remain dated
evidence from the repaired build, not a claim that the server is currently ready.

## Repair files and preserved boundaries

Application: `_ChatSources.cshtml`, `ChatEmbed.cshtml`, `ai-chat.js`, `ai-chat.css`,
`LearnerSourceLink.cs`, `TemplateAnswerComposer.cs`. Tests:
`ChatLearningAssistantTests`, `AiRagPhase6UiResilienceTests`,
`TemplateAnswerComposerTests`, changed wording expectations in
`AiBaselineRegressionTests`/`AiRagBoundaryAccessTests`/
`AiDeterministicBaselineIntegrationTests`/`AiRagPhase2FixRound2Tests`, and
`test_ai_chat_phase6_sources.cjs`. Tooling: `.agents/hooks.json`,
`agent_clients.py`, `check_team_hooks.py`, new `test_antigravity_hook_launch.py`.
Documentation: this ledger, review closure, repair plan, hook documentation and
bug memory. Existing scripts already handle direct-file invocation and validators
already compare against the canonical generator; no policy-handler rewrite was
needed for T1.

Grounding DTO/enums, source/access/ownership checks, quotas, request semantics,
security decisions, lesson data, application configuration and saved history are
unchanged by this repair. No new branch, subagent, paid call, commit, push or
deployment was used.

## Historical initial submission — superseded, retained as evidence

<details>
<summary>Original report before independent review and this repair; its claims are not current acceptance results</summary>

# Friendly chatbot presentation, icons and clean answer layout execution ledger

Date: 2026-11-17. Local branch: `fix/writing-goals-profile-polish`.
Starting HEAD: `9bc9fc885f52de2b44c526c6c56dac71ed033a62`; uncommitted baseline preserved.
Owner authorized local friendly chatbot presentation and layout cleanup. One agent coordinated, implemented and verified. No Git publication or release stage was performed.

## Result and maintained source

The learner-facing presentation of the TCT English chatbot was overhauled across full chat, the embedded launcher iframe, streaming, and history reloads:

1. **Complete removal of routine technical status/source UI**:
   - Technical status labels (`LearnerChatState.Label`, e.g. "Trả lời từ dữ liệu trong hệ thống", "Đã trả lời từ nguồn được phép xem") are completely eliminated from visible HTML and DOM across server-rendered Razor history (`_ChatSources.cshtml`), streaming completions, HTTP responses, and loaded older messages (`ai-chat.js`).
   - The visible "Nguồn" heading and raw source list (`<ul class="ai-chat-source-list">`) are completely removed. Assistive technology is spared routine metadata clutter.
   - Material content-change and snapshot notices (`Model.Notice`) are retained as concise friendly inline notices (`<p class="ai-chat-source-notice">`) only when actually present.
   - The `.ai-chat-sources` container has its top divider and empty spacing removed (`:empty { display: none; }` in `ai-chat.css`).

2. **Safe learning CTAs with restrained friendly icons**:
   - Useful navigation action buttons (`<a class="ai-chat-action">`) are retained (up to 3 read-only routes, `target="_top"`, escaped from the iframe).
   - Each action button displays a restrained, accessible SVG icon mapped by route category (Speaking, Writing, Listening, Reading, Practice, Goals, Profile, Settings, and Default) marked `aria-hidden="true"`.
   - Action labels use friendly, human-readable Vietnamese (e.g., "Mở luyện nói", "Mở luyện viết", "Xem Goals và danh hiệu", "Mở cài đặt tài khoản").
   - Data provenance attribute `data-source-key` is preserved on `<a class="ai-chat-action">` for integration and test traceability.
   - Action buttons are strictly derived from revalidated grounding sources with `freshness === "current"`. Stale or revoked sources never offer action buttons.

3. **Warm, cute, natural "mình–bạn" Vietnamese across all reply families**:
   - Authored assistant copy in `TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs` was completely revised from formal/technical phrasing into a kind study companion voice.
   - Restrained decorative icons from a coherent palette (`👋 🌱 📚 🎙️ ✍️ 🎧 📖 🎯 🏆 🔎`) are placed naturally without spamming every line or metric.
   - Starter guide bodies in `TCTEnglish/wwwroot/data/ai/website-guides.json` (bumped to version `2.1.0`) were polished with friendly phrasing.
   - Prompt/credential extraction refusals and security policies remain calm, firm, and unadorned.

4. **Architectural and security integrity**:
   - Uncommitted baseline work on `fix/writing-goals-profile-polish` is completely intact.
   - Grounding DTOs (`ChatGroundingDto`, `ChatGroundingSourceDto`, `stateLabel`), evidence hashes, access control, and IDOR boundary checks are preserved without changes to backend contracts.
   - `Program.cs`, `appsettings.json`, `.csproj`, shared layout, and global CSS tokens remain protected and unmodified.

## Subagent delegation status

Under the AGENTS.md execution policy update (2026-11-17), Antigravity with Gemini 3.8 Flash High may optionally use native subagents. Native invocation via `invoke_subagent` was attempted; however, a local pre-tool JSON hook failed (`jsonhook__tct-project-checks_PreToolUse_0_0 failed: command failed: exit status 1, stderr: [eval]:1 "const ^^^^^^ Unterminated string constant SyntaxError: Invalid or unexpected token"`).

As mandated by AGENTS.md and the plan instructions ("If the tools are unavailable or a hook rejects the call, continue within the coordinating agent. Do not claim native delegation was verified just because repository policy allows it"), no subagents or external worker processes were used. All implementation, review, test reconciliation, and verification were performed directly by the coordinator.

## Copy matrix: Before and after

| Reply family / Situation | Baseline copy (formal / technical) | Friendly presentation (warm, cute, companion) |
| --- | --- | --- |
| **Greeting** | "Chào bạn! 👋 Mình là trợ lý TCT English. Mình có thể giúp bạn: - Xem bộ từ vựng và tiến độ học..." | "Chào bạn! 👋 Mình là trợ lý TCT English. Hôm nay mình cùng luyện kỹ năng nào nhỉ? - Xem bộ từ vựng và tiến độ học 📚 - Gợi ý bài speaking phù hợp 🎙️ - Tra nghĩa từ trong bộ thẻ của bạn 🔎 - Hướng dẫn cách sử dụng website 🌱..." |
| **My Vocabulary (has sets)** | "Hiện tại bạn có X bộ từ vựng..." | "Hiện tại bạn có X bộ từ vựng nè 📚..." |
| **My Vocabulary (empty)** | "Hiện tại mình chưa tìm thấy bộ từ vựng nào trong tài khoản của bạn." | "Hiện tại mình chưa tìm thấy bộ từ vựng nào trong tài khoản của bạn 🌱 Bạn có thể tạo bộ từ đầu tiên tại trang **Bộ từ vựng** → **Tạo bộ từ mới** để bắt đầu nhé!" |
| **Card lookup (found)** | "Mình tìm thấy từ **term** trong bộ thẻ của bạn..." | "Mình tìm thấy từ **term** rồi nè 📖..." |
| **Card lookup (not found)** | "Mình không tìm thấy từ \"X\" trong các bộ từ vựng của bạn." | "Mình không tìm thấy từ \"X\" trong các bộ từ vựng của bạn nhé 🔎 Bạn có thể thêm từ này vào bộ từ mới hoặc tìm kiếm với từ khác nhé!" |
| **General starter recommendations** | "Mình chưa có đủ dữ liệu học để đưa ra gợi ý cụ thể... Dựa trên dữ liệu học của bạn..." | "Mình gợi ý vài lựa chọn nhẹ nhàng để khởi đầu hôm nay nhé 🌱 - **Vocabulary Starter**: ... - **Speaking Starter**: ... - **Writing Starter**: ... Đây là gợi ý khởi đầu chung để bạn làm quen nha. Bạn có thể chọn ngay kỹ năng muốn luyện ở các nút bên dưới nhé!" |
| **Personal study recommendations** | "Dựa trên dữ liệu học của bạn, mình gợi ý: Bộ X còn Y từ cần ôn..." | "Hôm nay chúng mình cùng bắt đầu với những gợi ý học tập này nhé 🌱 Bộ **X** còn **Y thẻ** cần ôn nè 📚. Mình học tiếp một chút nhé!" |
| **Goals summary** | "Goals hôm nay của bạn: ... Bạn có thể xem chi tiết tại: /Goals." | "Goals hôm nay của bạn: ... Bạn có thể xem chi tiết Goals qua nút bên dưới nhé 🎯" |
| **Speaking suggestions** | "Gợi ý bài speaking phù hợp:... Bạn có thể vào trang **Speaking** để duyệt toàn bộ..." | "Hôm nay mình cùng luyện nói một chút nhé 🎙️... Bạn có thể vào trang **Speaking** để duyệt toàn bộ playlist và video theo trình độ nha." |
| **Website guide** | "[Guide body]\n\nBạn có thể truy cập ngay tại: [route]" | "[Guide body]\n\nBạn có thể mở tính năng này qua nút gợi ý bên dưới nhé 🌱" |
| **Out of scope** | "Hiện tại mình chỉ hỗ trợ câu hỏi liên quan đến dữ liệu và tính năng của TCT English..." | "Hiện tại mình chỉ hỗ trợ câu hỏi liên quan đến dữ liệu và tính năng của TCT English như bộ từ vựng, tiến độ học, lớp học, speaking và cách sử dụng website 🌱 Bạn có thể hỏi theo các dạng như: - Tôi có những bộ từ vựng nào? 📚 - Tiến độ học của tôi ra sao? 🎯 - Gợi ý bài speaking phù hợp 🎙️ - Cách tạo lớp học hoặc tạo set 🌱" |
| **Refusal / Prompt injection** | Refusal text unchanged (calm, honest, secure) | Refusal text unchanged (no decorative icons or celebratory tone) |

## Changed files

| File | Nature of changes |
| --- | --- |
| `TCTEnglish/Services/AI/Rag/LearnerSourceLink.cs` | Added `ActionIcon(string? route)` method mapping allowed routes to scoped SVGs for speaking, writing, listening, reading, practice, goals, profile, settings, and default. |
| `TCTEnglish/Views/Ai/_ChatSources.cshtml` | Completely removed `.ai-chat-state` label and `.ai-chat-source-list`. Renders `<section class="ai-chat-sources">` only when `showNotice` or `actions.Count > 0`. Added action icon and label spans with `data-source-key`. |
| `TCTEnglish/wwwroot/js/ai-chat.js` | Updated `attachGrounding` to remove routine technical labels and source list. Only renders `.ai-chat-sources` when notices or safe actions exist. Added `actionIconSvg(route)`. |
| `TCTEnglish/wwwroot/css/ai-chat.css` | Added `.ai-chat-sources:empty { display: none; }`, removed top border/margin clutter, styled `.ai-chat-action` flex layout with gap and hover transition. |
| `TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs` | Polished assistant copy across all reply families into warm "mình–bạn" companion tone with restrained icons (`👋 🌱 📚 🎙️ ✍️ 🎧 📖 🎯 🏆 🔎`). Replaced raw URL lines with friendly CTA invitations. |
| `TCTEnglish/wwwroot/data/ai/website-guides.json` | Bumped version to `2.1.0`. Polished bodies of starter guides (`starter-vocabulary`, `starter-speaking`, `starter-writing`) with friendly tone. |
| `TCTEnglish.Tests/TemplateAnswerComposerTests.cs` | Updated expectations for friendly CTA invitation lines and icon-enhanced bullets. |
| `TCTEnglish.Tests/ChatLearningAssistantTests.cs` | Updated history reload assertions to verify absence of `ai-chat-source-link` and presence of `ai-chat-action`. |
| `scripts/tests/test_ai_chat_phase6_sources.cjs` | Adapted assertions for older messages, stream completion, and internal chat success to verify absence of routine technical state labels and presence of encoded action buttons. |
| `TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` | Reconciled route assertions to verify grounding destinations and friendly CTA guidance text. |
| `TCTEnglish.Tests/AiRagPhase2FixRound1Tests.cs` | Reconciled password route assertion to verify evidence authorized route. |
| `TCTEnglish.Tests/AiRagPhase2FixRound2Tests.cs` | Reconciled password and goals assertions to verify evidence authorized route and friendly CTA invitation. |
| `TCTEnglish.Tests/AiDeterministicBaselineIntegrationTests.cs` | Updated website guide and quick action tests to verify grounding sources for routes and friendly CTA guidance in body. |
| `TCTEnglish.Tests/AiProductionClassifierIntegrationTests.cs` | Updated quick action guide tests to verify grounding sources for routes. |
| `TCTEnglish.Tests/AiRagPhase6UiResilienceTests.cs` | Reconciled outcome state test to verify absence of routine `Answered` state label and proper notice rendering on changed content. |

## Verification evidence

| Check | Suite / Command | Observed result |
| --- | --- | --- |
| AI Chat Phase 6 Sources Node test | `node scripts/tests/test_ai_chat_phase6_sources.cjs` | **11 passed, 0 failed** (safe actions, no routine state clutter, hostile title encoding, stream buffering) |
| AI Chat Clay UI & Logo Node test | `node scripts/tests/test_ai_chat_clay_ui_and_logo.cjs` | **6 passed, 0 failed** (Clay theme wrapper, standalone embedded frame, floating launcher) |
| AI Chat Load Older Messages Node test | `node scripts/tests/test_ai_chat_load_older_order.cjs` | **2 passed, 0 failed** (chronological order, stable scroll anchor) |
| AI Chat Submit Guard Node test | `node scripts/tests/test_ai_chat_submit_guard.cjs` | **5 passed, 0 failed** (concurrent send guard, draft preservation, IME composition) |
| Chat Learning Assistant & Baseline Tests | `dotnet test --filter "...ChatLearningAssistantTests|...TemplateAnswerComposerTests|...AiBaselineRegressionTests|...WebsiteGuideRetrieverTests"` | **99 passed, 0 failed** |
| Broad RAG Boundary & Integration Tests | `dotnet test --filter "...AiRagBoundaryAccessTests|...AiRagPhase2FixRound1Tests|...AiRagPhase2FixRound2Tests|...AiDeterministicBaselineIntegrationTests|...AiProductionClassifierIntegrationTests|...AiRagPhase6UiResilienceTests"` | **126 passed, 0 failed** |
| UI & iframe escape validation | Inspection of `_ChatSources.cshtml` and `ai-chat.js` | All action buttons have `target="_top"`, safe route regex allowlist, and `data-source-key` |
| XSS prevention on hostile titles | `AiRagPhase6UiResilienceTests` & `test_ai_chat_phase6_sources.cjs` | Titles with `<img src=x onerror=alert(1)>` are strictly HTML-encoded as text; no image DOM element or script execution |

## Remaining limitations

1. **Native subagents**: Antigravity subagent invocation was blocked by a local pre-tool JSON hook syntax error (`jsonhook__tct-project-checks_PreToolUse_0_0`). All work was successfully completed inline by the coordinator.
2. **Native zoom 200%**: Emulated viewport widths (1440, 768, 390) and text overflow checks pass; physical browser 200% zoom with screen magnification was not visually observed in this turn.
3. **External AI providers**: No live OpenAI API requests (`gpt-6-luna`, `gpt-transcribe`) were made; all tests ran against deterministic and blocked test fakes per project boundaries.

</details>
