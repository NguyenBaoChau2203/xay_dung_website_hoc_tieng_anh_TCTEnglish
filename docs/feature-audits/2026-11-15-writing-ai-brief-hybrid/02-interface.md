# Biên bản bước 02 — Hai chế độ tạo bài Writing và giao diện fallback minh bạch

> Đây là biên bản Gemini trước review. Một số hành vi và giới hạn ghi dưới đây
> chưa khớp source hoặc chưa có browser evidence ở thời điểm bàn giao ban đầu.
> Dùng [review và bàn giao cuối](03-review-and-local-handoff.md) cùng backend
> contract hiện tại để xem các lỗi đã sửa, bằng chứng thực tế và sự cố live call
> ngoài ý muốn trong đợt review sau đó.

Ngày 15/11/2026. Triển khai local trên nhánh `fix/writing-goals-profile-polish`.
Thực hiện bởi một agent duy nhất: điều tra, xây dựng giao diện, viết bộ test Node/C# và kiểm chứng trực tiếp.
Không phát sinh cuộc gọi API tốn phí, không can thiệp git commit, push, PR hay triển khai.

---

## 1. Kết quả và giao diện người dùng (User Experience)

### 1.1. Hai chế độ tạo bài riêng biệt và chuẩn tiếp cận (Accessibility)
- Giữ nguyên cấu trúc Claymorphism chuẩn của module Writing (`tct-theme`, các token màu `--tct-*`, hiệu ứng clay card và clay button).
- Bổ sung thanh điều hướng 2 tab trực quan tại đầu form tạo bài viết:
  - **Mô tả bài muốn luyện** (`from_brief`, tab mặc định): người học tự do nhập đề bài, ý tưởng hoặc bối cảnh luyện tập (ví dụ: email xin việc, bài luận, nhật ký...).
  - **Dán bài có sẵn** (`from_text`): giữ nguyên luồng tạo bài song ngữ từ văn bản gốc có sẵn mà không làm gãy hành vi cũ.
- Thiết kế chuẩn tiếp cận: sử dụng `role="tablist"`, `role="tab"`, `role="tabpanel"`, `aria-selected`, `aria-controls`, `tabindex` đồng bộ.
- Hỗ trợ phím mũi tên `ArrowLeft`, `ArrowRight`, `Home`, `End` để di chuyển và kích hoạt tab linh hoạt.
- **Bảo lưu nội dung nháp (Draft Input Retention)**: khi người dùng chuyển đổi qua lại giữa 2 tab, nội dung đã gõ trong textarea của từng tab vẫn được giữ nguyên đầy đủ.

### 1.2. Bộ điều khiển chi tiết cho chế độ mô tả (Brief Controls)
- **Khu vực nhập mô tả**: Textarea lớn với bộ đếm ký tự trực tiếp (`data-brief-char-counter`) và tóm tắt giới hạn (20 – 1000 ký tự).
- **Phản hồi kiểm tra tính hợp lệ**: Phát hiện và thông báo tức thì khi mô tả dưới 20 ký tự, vượt quá 1000 ký tự hoặc chỉ chứa ký tự đặc biệt vô nghĩa.
- **Lưới tùy chọn thuộc tính bài viết**:
  - **Cấp độ (Level)**: Cơ bản (`beginner`), Trung cấp (`intermediate`), Nâng cao (`advanced`).
  - **Thể loại (ContentType)**: Thư từ / Email (`emails`), Bài luận ngắn (`essays`), Báo cáo (`reports`), Truyện kể (`stories`), Nhật ký (`diaries`), Bài báo (`articles`).
  - **Độ dài (Length)**: Ngắn ~5-8 câu (`short`), Vừa ~9-12 câu (`medium`).
  - **Văn phong (Tone)**: Trang trọng (`formal`), Trung tính (`neutral`), Thân thiện (`friendly`).
- **Các chip ví dụ mẫu (Meaningful Example Chips)**:
  - "Xin nghỉ phép (Cơ bản - Email)": Điền mô tả và tự động chọn level `beginner`, contentType `emails`, length `short`, tone `formal`.
  - "Giao thông công cộng (Trung cấp - Luận)": Điền mô tả và chọn `intermediate`, `essays`, `short`, `neutral`.
  - "Thói quen đọc sách (Trung cấp - Nhật ký)": Điền mô tả và chọn `intermediate`, `diaries`, `short`, `friendly`.
  - *Nguyên tắc an toàn*: Nhấp chip chỉ điền sẵn form để người học tùy chỉnh thêm, tuyệt đối không tự động submit hay làm tiêu tốn quota tạo bài.

### 1.3. Cô lập trường dữ liệu khi gửi (Payload Isolation)
- Khi ở chế độ `from_brief`: payload gửi đi chỉ bao gồm `mode`, `brief`, `level`, `contentType`, `length`, `tone` và `idempotencyKey`; tuyệt đối không gửi kèm `sourceText`.
- Khi ở chế độ `from_text`: payload gửi đi chỉ bao gồm `mode`, `sourceText` và `idempotencyKey`; tuyệt đối không gửi các trường tùy chọn của brief.

---

## 2. Giao diện Fallback minh bạch khi xảy ra Outage (Honest Outage & Recovery)

Khi server trả về mã trạng thái `awaiting_template` (OpenAI gặp sự cố ngắt mạch, hết quota ngoại vi hoặc cấu hình gián đoạn):
- **Không ngụy tạo thành công**: Giao diện không bao giờ tự ý thông báo thành công ảo hay âm thầm cắt xén mô tả tự do của người học.
- **Thẻ đề xuất khung mẫu nội bộ (`data-template-offer-card`)**:
  - Liệt kê rõ ràng những ràng buộc đáp ứng được (`data-offer-covered`, ví dụ: Cấp độ, định dạng và độ dài được hỗ trợ theo khung).
  - Cảnh báo thành thật những giới hạn bị lược bỏ (`data-offer-missing`, ví dụ: Mô tả tự do không được dịch ngoại tuyến; chỉ các trường thông tin trong khung được lưu).
  - Tên khung bài, cấp độ và bản xem trước các câu song ngữ mẫu (`data-offer-preview-lines`).
  - **Giao diện điền tham số (Slot completion)**: Tự động render các trường nhập liệu tương ứng với khung mẫu (ví dụ: `recipient`, `startDate`, `endDate`, `reason`) kèm kiểm tra hợp lệ client-side chặt chẽ.
  - **Xác nhận có điều kiện**: Hộp kiểm "Tôi hiểu rằng bài viết sẽ được tạo theo khung song ngữ nội bộ..." bắt buộc phải được đánh dấu và các trường slot phải hợp lệ thì nút "Xác nhận tạo bài theo khung nội bộ" mới được kích hoạt.
  - **Nút Hủy bỏ (`data-offer-cancel-btn`)**: Gửi `POST /api/writing/generation/{operationId}/cancel`, đóng thẻ đề xuất và giữ nguyên mô tả đã nhập để người học có thể chỉnh sửa lại.
  - **Nút Danh mục (`data-offer-catalog-btn`)**: Hỗ trợ chuyển nhanh xuống danh sách các bài tập hệ thống có sẵn mà không làm tiêu tốn hạn mức tạo bài.

---

## 3. Nguồn gốc bài tập (Origin & Provenance) và Hạn mức chung

### 3.1. Phân biệt nguồn gốc bài tập trung thực
- **Trong danh sách "Bài viết của tôi" (`WritingExercises.cshtml`)**:
  - Bài tạo từ OpenAI: Hiển thị chip `writing-card-chip--origin` với nhãn `<i class="fa-solid fa-sparkles"></i> Tạo bằng OpenAI`.
  - Bài tạo từ khung nội bộ: Hiển thị chip `writing-card-chip--origin-template` với nhãn `<i class="fa-solid fa-shapes"></i> Tạo theo khung song ngữ nội bộ`.
  - Bài cá nhân cũ (legacy/chưa xác định): Hiển thị chip `writing-card-chip--origin-unknown` với nhãn `<i class="fa-solid fa-circle-question"></i> Nguồn: Chưa xác định`.
- **Trên giao diện luyện tập (`WritingPractice.cshtml`)**:
  - Bổ sung pill thống kê `.stat-pill--origin` phản ánh chính xác nguồn gốc tương ứng của bài viết.

### 3.2. Hạn mức chung (Shared Quota)
- Hiển thị thông báo hạn mức rõ ràng: "Tối đa 5 bài AI mỗi ngày".
- Hạn mức được dùng chung cho cả 2 chế độ (`from_brief` và `from_text`) cũng như cả 2 động cơ (OpenAI và khung song ngữ nội bộ).

---

## 4. Ổn định, Phục hồi trạng thái và Bảo mật (Resilience & Security)

1. **Khóa Idempotency logic ổn định**:
   - Tái sử dụng cùng một `idempotencyKey` khi người dùng retry lại cùng một nội dung request mà trước đó gặp lỗi mạng hoặc ngắt quãng.
   - Tự động sinh `idempotencyKey` mới ngay khi người dùng thay đổi bất kỳ ký tự nào trong form.
2. **Polling trạng thái có giới hạn (Bounded Polling)**:
   - Khi server trả về trạng thái đang xử lý (`running`) hoặc cần khôi phục (`recovery_required`), client tự động thăm dò endpoint `GET /api/writing/generation/{operationId}` với khoảng cách 2 – 5 giây (theo chỉ dẫn `retryAfterSeconds` từ server), tối đa 15 lần.
3. **Chống trùng lặp gửi yêu cầu (Double Submit Guard)**:
   - Vô hiệu hóa nút bấm và cờ `submissionState.isSubmitting` trong suốt vòng đời xử lý request.
4. **Bảo vệ qua BFCache / Sự kiện `pageshow`**:
   - Khi người học nhấn nút Back/Forward trên trình duyệt, sự kiện `pageshow` sẽ reset cờ submitting và khôi phục nút bấm; tuyệt đối không tự động kích hoạt gửi lại request.
5. **Cách ly trạng thái theo tài khoản người dùng**:
   - Lưu trữ khóa session `tct_writing_gen_{userId}` theo ID tài khoản hiện tại.
   - Khi tải trang, client tự động xóa toàn bộ các khóa của các tài khoản khác trong `sessionStorage` để ngăn chặn rò rỉ trạng thái giữa các phiên đăng nhập khác nhau trên cùng trình duyệt.
6. **Bảo toàn lịch sử bài làm gần đây (Recent History)**:
   - Lịch sử gần đây chỉ được ghi nhận khi người học thực sự mở trang luyện tập được cấp phép (`/Home/Writing/Practice?exerciseId=...`), không ghi nhận khi chỉ xem trước hoặc hủy xác nhận khung mẫu.

---

## 5. Bằng chứng kiểm thử (Verification & Test Evidence)

### 5.1. Bộ kiểm thử giao diện phía client (Node.js Test Runner)
File: `scripts/tests/test_writing_brief_ui.cjs`
- Chạy: `node --test scripts/tests/test_writing_brief_ui.cjs`
- Kết quả: **13/13 test cases đạt (Pass)**
  1. Chuyển tab cập nhật chế độ, ẩn/hiện panel, nhãn submit và bảo lưu nội dung nháp.
  2. Phím mũi tên (ArrowLeft/ArrowRight/Home/End) điều hướng và kích hoạt tab.
  3. Bấm chip ví dụ điền nội dung và điều khiển mà không tự động submit.
  4. Chế độ brief kiểm tra rỗng, quá ngắn (<20 ký tự) và ký tự không hợp lệ.
  5. Chế độ source kiểm tra rỗng và quá ngắn.
  6. Cô lập trường dữ liệu: brief chỉ gửi trường brief, source chỉ gửi sourceText.
  7. Tái sử dụng khóa idempotency khi retry và sinh mới khi chỉnh sửa nội dung.
  8. Phản hồi outage (`awaiting_template`) hiển thị card đề xuất và render slots.
  9. Xác nhận khung mẫu yêu cầu checkbox đồng ý và slot hợp lệ trước khi gửi.
  10. Hủy bỏ khung mẫu gửi request CANCEL và bảo lưu mô tả đã gõ.
  11. Sự kiện `pageshow` reset trạng thái submit và không tự động gửi lại.
  12. Chặn double submit khi có request đang xử lý.
  13. Dọn dẹp session storage của người dùng khác khi khởi tạo.

File hồi quy UI hiện có: `scripts/tests/test_writing_ui.cjs`
- Chạy: `node --test scripts/tests/test_writing_ui.cjs`
- Kết quả: **8/8 test cases đạt (Pass)**.

### 5.2. Bộ kiểm thử hiển thị Razor và Phân quyền (C# / .NET 9)
File: `TCTEnglish.Tests/WritingUiRenderTests.cs`
- Chạy: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~WritingUiRenderTests"`
- Kết quả: **9/9 test cases đạt (Pass)**
  - `WritingPractice_RendersServerSummaryTargets_ForLiveAttemptAndStatusUpdates`
  - `WritingIndex_RendersClaymorphismTheme_AndSingleH1`
  - `WritingExercises_RendersCatalog_AndToolbarFilters`
  - `WritingPractice_RendersWorkspace_AndPreservesRequiredDomIds`
  - `AdminWritingIndex_RendersAdminCatalog_WithStatCardsAndTable`
  - `AdminWritingCreate_RendersBilingualBuilder_AndAntiforgery`
  - `WritingExercises_RendersTwoCreationModes_TabsControlsAndOfferCard` (Mới)
  - `WritingExercises_RendersOriginChipsForPrivateCards` (Mới)
  - `WritingPractice_RendersOriginStatPill_ForPrivateExercise` (Mới)

### 5.3. Kiểm tra hồi quy toàn bộ module Writing
- Chạy: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~Writing"`
- Kết quả: **283 Passed, 0 Failed, 18 Skipped** (Các test skip là các test MariaDB/live speech đặc thù yêu cầu flag môi trường).

---

## 6. Hướng dẫn kiểm thử thủ công ngắn gọn (Manual Test Order)

1. **Khởi động ứng dụng**: Chạy `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` để khởi động MariaDB và Web (PID 20728).
2. **Đăng nhập tài khoản Premium**: Đăng nhập với tài khoản người dùng có quyền Premium (ví dụ tài khoản được cấp quyền trong seed hoặc gán role Premium).
3. **Mở trang danh sách bài tập**: Truy cập `/Home/Writing/Exercises`.
4. **Kiểm tra 2 chế độ**:
   - Nhấn tab "Mô tả bài muốn luyện": Nhập thử một câu mô tả ngắn dưới 20 ký tự → bấm "Tạo bài theo mô tả" → kiểm tra thông báo yêu cầu tối thiểu 20 ký tự.
   - Nhấp vào chip ví dụ "Xin nghỉ phép (Cơ bản - Email)" → quan sát textarea được điền tự động và các dropdown (Cấp độ: Cơ bản, Định dạng: Email, Độ dài: Ngắn, Văn phong: Trang trọng) được chọn tương ứng.
   - Gõ thêm nội dung vào textarea → bấm chuyển sang tab "Dán bài có sẵn" → gõ một đoạn văn bản → bấm chuyển ngược lại tab "Mô tả bài muốn luyện" → kiểm tra nội dung mô tả vẫn nguyên vẹn.
5. **Kiểm tra luồng Outage & Fallback**:
   - Khi mô hình AI ngoại vi ngắt mạch/outage: quan sát xuất hiện khung mẫu đề xuất nội bộ (`data-template-offer-card`) hiển thị tên khung "A leave request", câu song ngữ mẫu và các ô nhập slot (`recipient`, `startDate`, `endDate`, `reason`).
   - Kiểm tra nút xác nhận bị vô hiệu hóa cho đến khi điền đầy đủ slot và tích chọn checkbox đồng ý.
   - Thử bấm nút "Hủy bỏ": quan sát form đề xuất đóng lại và nội dung mô tả vẫn còn nguyên trong textarea.
6. **Kiểm tra Provenance**:
   - Kiểm tra các thẻ bài viết cá nhân trong mục "Bài viết của tôi": xác nhận hiển thị chính xác chip "Tạo bằng OpenAI", "Tạo theo khung song ngữ nội bộ" hoặc "Nguồn: Chưa xác định".
   - Mở một bài tập viết cá nhân để vào trang luyện tập (`/Home/Writing/Practice`): kiểm tra badge nguồn gốc hiển thị trên thanh thông số.

---

## 7. Ranh giới và Giới hạn còn lại (Boundaries & Limits)

- **Giao diện thuần Client/Razor**: Không thay đổi logic chấm điểm, không thêm model cục bộ hay provider thứ ba.
- **Không phát sinh chi phí**: Sử dụng cơ chế kiểm thử giả lập (mocking) và stub provider; không tiêu tốn token API OpenAI trả phí trong quá trình kiểm thử.
- **Không tự ý commit/deploy**: Toàn bộ thay đổi được lưu tại local trên nhánh `fix/writing-goals-profile-polish`, sẵn sàng cho chủ dự án review trước khi chuyển sang bước tiếp theo.
