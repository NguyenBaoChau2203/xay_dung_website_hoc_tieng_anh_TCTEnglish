# Checklist test tay staging — phần việc của Châu sau đăng nhập

Ngày chuẩn bị: 11/11/2026, Asia/Bangkok.

Mục đích: xác định đúng phạm vi Châu phụ trách và thứ tự test giao diện/chức
năng trên staging. Chủ dự án test tay; Codex điều tra và sửa backend khi có báo
lỗi, Gemini xử lý giao diện theo phân công của chủ dự án.

Trạng thái: checklist chưa thực hiện. Lượt chuẩn bị chỉ đối chiếu README, tài
liệu phạm vi và controller hiện tại trên checkout `master`; chưa mở staging,
chưa xác nhận phiên bản triển khai, chưa chạy test ứng dụng và chưa sửa code.

## Phạm vi và căn cứ

- [Phân công nhóm](../../../README.md): Account/bảo mật/SMTP, Speaking, Writing,
  Goals/huy hiệu, AI Chat, Admin Dashboard/người dùng/học liệu Speaking-Writing/AI.
- [Danh mục màn hình đã đối chiếu](2026-11-08-chau-ui-design-system-scope.md):
  dùng để xác định ranh giới; các checkbox triển khai lịch sử không phải kết quả
  test staging hôm nay.
- Entry points hiện tại đã đối chiếu bằng CodeGraph:
  `AccountController.Profile/Settings`, `SpeakingController.Index/Practice`,
  `StudyController.Speaking/Writing/WritingExercises/WritingPractice`,
  `GoalsController.Index`, `AiController.Chat`.
- Chi tiết luồng tham khảo các execution ledger Speaking/Writing ngày 09/11,
  Goals ngày 08/11 và AI/Admin ngày 10/11 trong `docs/feature-audits`.

Vòng này bỏ đăng nhập, đăng ký, OAuth, quên/đặt lại mật khẩu và các ca auth/SMTP
riêng. Giữ Hồ sơ và Cài đặt vì là màn sau đăng nhập; không cần đổi thông tin xác
thực để hoàn thành lượt test này. Phân quyền tính năng vẫn được kiểm tra bằng
phiên người dùng đã đăng nhập.

Dashboard học viên, Reading, Listening, Vocabulary, CRUD bộ thẻ/thư mục, lớp học,
chat lớp, tìm kiếm toàn cục, thanh toán và Notifications thuộc phần đồng đội.
Chỉ đi qua nếu cần vào một chức năng của Châu; không mở rộng thành test toàn bộ
module. AI Chat dùng hạ tầng SignalR không làm chat lớp thành phần Châu phụ trách.
Dashboard Admin thuộc phạm vi; Dashboard học viên không thuộc phạm vi.

## Chuẩn bị

- [ ] Dùng tài khoản thử đã đăng nhập. Nếu có sẵn, dùng Standard và Premium để
  so sánh tính năng/quota; dùng Admin cho khu quản trị. Chỉ test bằng Admin không
  chứng minh trải nghiệm Standard/Premium.
- [ ] Mở `/Goals` và ghi lại mục tiêu, tiến độ Speaking/Writing, XP, streak và
  huy hiệu hiện tại để so sánh sau khi học. Không mặc định mọi thao tác đều cộng
  XP hay hoàn thành mục tiêu.
- [ ] Chọn video và bài Writing có sẵn để làm trước. Có sẵn một bộ thẻ được phép
  truy cập nếu test Speaking theo thẻ.
- [ ] Với CRUD Admin, dùng nội dung thử có dấu hiệu nhận biết. Không sửa/xóa bài
  thật để chứng minh chức năng; xóa nội dung thử sau khi đã kiểm tra ở phía học viên.

## Thứ tự thực hiện

| Chặng | Trang / luồng | Thao tác và kết quả cần quan sát |
| --- | --- | --- |
| 01 | `/Account/Profile`, `/Account/Settings` | Đúng người dùng/thông tin; mở và đóng hộp sửa; lưu thay đổi hồ sơ/ảnh trên tài khoản thử; tải lại còn dữ liệu; dữ liệu không hợp lệ có thông báo rõ. Quan sát cài đặt nhưng bỏ quy trình đổi thông tin xác thực trong vòng này. |
| 02 | `/Speaking` và Video của tôi | Tìm kiếm, lọc cấp độ/chủ đề, xem thêm; không lặp/mất video. Thử thêm video riêng bằng URL hợp lệ và không hợp lệ; trạng thái import có kết thúc hoặc báo lỗi rõ; video sẵn sàng mở được; xóa video thử của mình. |
| 03 | `/Speaking/Practice/{id}` | Phát/dừng, tốc độ, chọn/lặp câu và đồng bộ phụ đề; cấp mic, ghi/dừng/nghe lại/chấm; thử đọc đúng, sai và im lặng. Chuyển chế độ nói/chép chính tả; kết quả lỗi không được thành công giả. Tải lại và mở lại để đối chiếu tiến độ đã lưu. |
| 04 | `/Home/Speaking/{setId}` | Vào luyện nói từ bộ thẻ có sẵn và được phép truy cập; chuyển thẻ, phát âm/ghi âm/chấm theo điều khiển hiện có; không lẫn nội dung giữa thẻ. Không mở rộng sang CRUD bộ thẻ. |
| 05 | `/Home/Writing` → `/Home/Writing/Exercises` | Chọn cấp độ/nội dung; lọc chủ đề/trạng thái; phân trang; tổ hợp không có bài có trạng thái rỗng; mở bài đúng lựa chọn. Thử Bài viết của tôi, tạo bài bằng AI và xóa bài thử; đối chiếu trạng thái khóa/quota theo gói. |
| 06 | `/Home/Writing/Practice?exerciseId=...` | Gửi câu đúng, câu sai, câu rỗng; xem gợi ý; gửi bằng nút và phím tắt hiện có; không nộp trùng khi bấm liên tiếp. Rời trang/tải lại để kiểm tra nháp và tiến độ. Phân biệt Accepted, NeedsRevision và NeedsReview; trạng thái cần xem lại không được giả thành đúng hoặc chắc chắn sai. |
| 07 | `/Goals` → `/Account/Profile` | So sánh với số liệu đầu buổi sau khi hoàn thành bài đủ điều kiện. Sửa mục tiêu, thử biên 0 và 500, giá trị ngoài khoảng; 0 tạm dừng mục tiêu kỹ năng và không xóa lịch sử. Tải lại và làm lại bài đã hoàn thành để phát hiện cộng thưởng trùng; chỉ kỳ vọng huy hiệu khi đạt điều kiện. |
| 08 | `/AI/Chat`, nút AI nổi và `/AI/Chat?embed=true` | Hỏi câu đầu/câu tiếp theo, tạo hội thoại mới, mở lịch sử, tải tin cũ nếu đủ dữ liệu, xóa hội thoại thử; mở nguồn tham chiếu khi có. Kiểm tra streaming/dừng, đóng-mở cửa sổ, quota và tình huống mạng lỗi. Nếu có nút thử lại, một thao tác không tạo hai lượt trả lời hoặc tính quota hai lần. |
| 09 | `/Admin/Dashboard` | Số liệu và biểu đồ hiển thị đúng nhãn, trạng thái tải/lỗi rõ; điều hướng và trạng thái menu đúng trang; popup/bảng không che mất thao tác. Chỉ đối chiếu số liệu tuyệt đối khi biết tập dữ liệu, bộ lọc và khoảng thời gian. |
| 10 | `/Admin/SpeakingVideoManagement` → Create/Edit | Tìm/lọc/phân trang; thêm video thử, nhập thiếu/sai để kiểm tra validation, nhập/trích phụ đề, sửa rồi mở lại. Khi nội dung sẵn sàng và được công bố đúng cơ chế hiện có, quay về Speaking học viên để kiểm tra phát/luyện; sau đó xóa nội dung thử. |
| 11 | `/Admin/WritingExerciseManagement` → Create/Edit | Thêm/sửa bài thử; tách/ghép câu Việt-Anh, cảnh báo lệch số câu, đáp án mẫu và biến thể đáp án được duyệt; lọc/phân trang. Quay lại Writing học viên để đối chiếu nội dung và chấm với đáp án vừa lưu; sau đó xóa bài thử. |
| 12 | `/Admin/AiManagement` → `/Admin/UserManagement` | AI: xem trạng thái cấu hình/dataset/chỉ mục và thông báo; các tác vụ Train/Rebuild/Embedding test riêng khi chủ đích kiểm tra vận hành, không bấm chỉ để thử giao diện. Users: tìm/lọc/phân trang, mở-sửa-lưu thông tin tài khoản thử, đổi vai trò và khóa/mở khóa tài khoản thử cuối cùng; kiểm tra trạng thái sau tải lại. Không khóa/đổi vai trò tài khoản Admin đang dùng để điều khiển lượt test. |

## Các ca tích hợp cần giữ

- [ ] Hoàn thành Speaking/Writing đủ điều kiện → Goals cập nhật → Profile chỉ hiện
  huy hiệu đã được trao. Tải lại/nộp lại không tự phát sinh thưởng trùng.
- [ ] Thêm/sửa học liệu Admin → trạng thái công bố đúng → học viên thấy và làm
  được đúng bài; bài riêng không tự trở thành học liệu chung.
- [ ] AI đầy đủ và cửa sổ nổi phản ánh đúng lịch sử/quota sau làm mới; lỗi hoặc
  lượt đang chờ không bị hiển thị như đã hoàn tất.
- [ ] Nếu có hai tài khoản thử, đối chiếu sự tách biệt video riêng, bài Writing
  riêng, lịch sử AI, nháp và tiến độ. Không dùng dữ liệu của người thật để thử.
- [ ] Trong mỗi chặng, quan sát trạng thái đang tải/rỗng/lỗi/thành công và thử
  tải lại sau lưu. Có thể kiểm tra bàn phím/zoom/kích thước màn hình trên cùng
  các thao tác; đây là ghi nhận UI để chuyển Gemini, không phải đã nghiệm thu.

## Ghi nhận kết quả

Đánh dấu từng chặng: chưa test / đạt / lỗi / bị chặn; có thể ghi một chặng lỗi
nhưng tiếp tục phần độc lập. Không có tài khoản/gói/dữ liệu/AI/mic phù hợp thì
ghi chưa kiểm chứng, không đánh dấu đạt.

Mẫu báo lỗi thủ công:

```text
Chặng: 03 — Speaking Practice
Trang/URL:
Vai trò/gói tài khoản thử:
Thời điểm gặp:
Các bước thao tác:
Mong đợi:
Thực tế:
Lặp lại được hay thỉnh thoảng:
Ảnh/video/thông báo lỗi (nếu có):
```

Không ghi mật khẩu, token, khóa API, cookie hay thông tin cá nhân vào báo lỗi.
Lỗi giao diện chuyển Gemini; lỗi xử lý/lưu dữ liệu/quota/quyền truy cập/chấm
bài chuyển Codex. Vấn đề giao thoa được điều tra trước để xác định bên cần sửa.
