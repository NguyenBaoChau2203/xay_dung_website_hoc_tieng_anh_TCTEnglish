# AI Chatbot Gemini Free Tier Owner Checklist

Tài liệu này dành cho bạn, không phải cho AI agent.

Mục tiêu của file này:

1. Chỉ rõ bạn cần tự làm gì để dùng `Gemini free tier`.
2. Chỉ rõ đăng ký ở đâu, tạo key ở đâu, và cần lưu ý gì.
3. Chỉ rõ sau khi AI agent làm xong các phase thì bạn cần điền gì vào đâu.
4. Giúp bạn kết thúc nhanh và merge vào `master` an toàn.

Tài liệu này được viết để khớp với:

- [ai-chatbot-gemini-phase-plan.md](ai-chatbot-gemini-phase-plan.md)

Hướng mặc định của nhánh này là:

- `Gemini-only`
- dùng `Gemini Developer API free tier`
- không giữ `OpenAI`
- không giữ `Ollama` trong merge-ready config

---

## 1. Bạn Có Đang Đi Đúng Hướng Free Tier Không?

Có.

Tính đến ngày `31/03/2026`, tài liệu chính thức của Google vẫn cho thấy:

1. Gemini Developer API có `free tier`.
2. Free tier có rate limits thấp hơn paid tier.
3. Rate limits được tính theo `project`, không theo API key.
4. Quota theo ngày reset vào `midnight Pacific time`.
5. Ở free tier, dữ liệu có thể được dùng để cải thiện sản phẩm của Google.

Nguồn chính thức:

- Pricing:
  [ai.google.dev/gemini-api/docs/pricing](https://ai.google.dev/gemini-api/docs/pricing)
- Billing overview:
  [ai.google.dev/gemini-api/docs/billing](https://ai.google.dev/gemini-api/docs/billing/)
- Rate limits:
  [ai.google.dev/gemini-api/docs/rate-limits](https://ai.google.dev/gemini-api/docs/rate-limits)

Điều này có nghĩa là:

- để làm website demo nhỏ cho bạn bè, `free tier` là hướng hợp lý
- nếu sau này bạn cần quota lớn hơn hoặc muốn dữ liệu không bị dùng để improve
  product, bạn có thể nâng lên paid tier mà không cần viết lại cả tính năng

---

## 2. Trước Khi Tạo Key, Bạn Cần Biết 3 Điều Quan Trọng

### Điều 1 - Bạn cần dùng được Google AI Studio

Bạn cần:

1. có tài khoản Google
2. ở khu vực được hỗ trợ
3. đáp ứng yêu cầu độ tuổi của Google AI Studio

Nguồn:

- Available regions:
  [ai.google.dev/gemini-api/docs/available-regions](https://ai.google.dev/gemini-api/docs/available-regions)
- Gemini API terms:
  [ai.google.dev/gemini-api/terms](https://ai.google.dev/gemini-api/terms)

Ghi chú quan trọng:

- trang available regions hiện tại có liệt kê `Vietnam`
- tài liệu terms hiện tại nêu rõ yêu cầu `18+`

### Điều 2 - Free tier là theo project

Nếu bạn tạo nhiều API key trong cùng 1 project thì vẫn dùng chung quota của project
đó.

Vì vậy:

- không nên nghĩ “tạo thêm key là có thêm quota”
- nên giữ số lượng key gọn, dễ quản lý

### Điều 3 - Bạn nên giữ hướng stable, không chạy theo preview nếu chưa cần

Để website demo nhỏ ổn định hơn, nên ưu tiên model stable thay vì preview model mới
nhất.

Với nhánh này, model để bắt đầu hợp lý là:

- `gemini-2.5-flash-lite`

Lý do:

- nhẹ hơn
- hợp với free tier
- đủ cho chatbot tutoring cơ bản
- ổn định hơn việc đẩy nhanh sang preview model chỉ vì “mới hơn”

Nguồn:

- Models:
  [ai.google.dev/gemini-api/docs/models](https://ai.google.dev/gemini-api/docs/models)
- Pricing:
  [ai.google.dev/gemini-api/docs/pricing](https://ai.google.dev/gemini-api/docs/pricing)

---

## 3. Bạn Tạo Gemini API Key Ở Đâu?

Bạn tạo và quản lý key trong `Google AI Studio`.

Đường dẫn chính:

- Google AI Studio:
  [aistudio.google.com](https://aistudio.google.com/)
- Hướng dẫn API key:
  [ai.google.dev/gemini-api/docs/api-key](https://ai.google.dev/gemini-api/docs/api-key)
- Quickstart:
  [ai.google.dev/gemini-api/docs/quickstart](https://ai.google.dev/gemini-api/docs/quickstart)

Theo tài liệu chính thức:

- bạn có thể tạo key miễn phí trong Google AI Studio
- mọi key gắn với một Google Cloud project
- nếu chưa có project, AI Studio có thể tạo default project cho user mới hoặc cho
  bạn import project vào

---

## 4. Các Bước Tự Làm Để Có Key

### Bước 1 - Đăng nhập Google AI Studio

Vào:

- [aistudio.google.com](https://aistudio.google.com/)

Sau đó:

1. đăng nhập bằng tài khoản Google của bạn
2. nếu hệ thống hiện Terms hoặc thông báo đầu tiên, hãy đọc và bấm chấp nhận
3. chờ AI Studio vào được trang chính

Nếu bị chặn:

1. kiểm tra khu vực có được hỗ trợ không
2. kiểm tra tài khoản có đáp ứng yêu cầu độ tuổi không
3. nếu dùng Workspace account, có thể cần admin mở quyền AI Studio

Nguồn:

- Available regions:
  [ai.google.dev/gemini-api/docs/available-regions](https://ai.google.dev/gemini-api/docs/available-regions)
- Workspace access:
  [ai.google.dev/gemini-api/docs/workspace](https://ai.google.dev/gemini-api/docs/workspace)

### Bước 2 - Tạo hoặc import project

Sau khi vào AI Studio:

1. tìm phần `API Keys` hoặc nút `Get API key`
2. nếu hệ thống yêu cầu chọn project:
   - chọn project sẵn có nếu bạn đã có
   - hoặc tạo project mới nếu chưa có
3. nếu hệ thống cho phép import project từ Google Cloud thì bạn cũng có thể dùng
   cách đó

Lưu ý:

- quota free tier là theo project
- vì vậy, nên chọn 1 project rõ ràng cho branch này

### Bước 3 - Tạo API key

Khi đã ở đúng trang key:

1. bấm `Create API key`
2. nếu hiện danh sách project, chọn đúng project bạn vừa chuẩn bị
3. chờ hệ thống tạo key
4. copy key ngay khi key hiện ra

Tên nút trên UI có thể thay đổi nhẹ theo từng đợt cập nhật, nhưng ý chính vẫn là:

- vào trang `API Keys`
- bấm tạo key mới
- chọn đúng project

Tài liệu chính thức về key và projects:

- [ai.google.dev/gemini-api/docs/api-key](https://ai.google.dev/gemini-api/docs/api-key)

### Bước 4 - Lưu key ở nơi riêng

Ngay sau khi tạo xong:

1. copy key
2. lưu vào password manager hoặc local secret store
3. không dán vào repo

Không dán key vào:

- `appsettings.json`
- `appsettings.Development.json`
- file `.md`
- commit git
- browser-side JavaScript

### Bước 5 - Nếu có thể, hãy giới hạn key

Nếu giao diện hoặc Google Cloud console có phần restriction phù hợp, hãy ưu tiên:

- chỉ cho key này dùng với `Generative Language API`

Điều này không bắt buộc để chạy local test, nhưng là thói quen tốt trước khi deploy.

---

## 5. Bạn Nên Đợi AI Agent Làm Đến Phase Nào Rồi Mới Điền Key?

Bạn nên làm theo thứ tự này:

1. Bảo AI agent làm `Phase 0`
2. Bảo AI agent làm `Phase 1`
3. Bảo AI agent làm `Phase 2`
4. Bảo AI agent làm `Phase 3`

Sau khi `Phase 3` xong, bạn mới nên điền key để chạy local test.

Sau đó:

5. Bảo AI agent làm `Phase 4`
6. Bảo AI agent làm `Phase 5`

Lý do:

- Phase 1-3 là giai đoạn chốt kiến trúc, provider và config sample
- nếu điền key quá sớm, bạn dễ bị test trên một bản code chưa ổn định

Lệnh mẫu:

- `Làm Phase 0 trong docs/ai-chatbot-gemini-phase-plan.md cho tôi`
- `Làm Phase 1 trong docs/ai-chatbot-gemini-phase-plan.md cho tôi`
- `Làm Phase 2 trong docs/ai-chatbot-gemini-phase-plan.md cho tôi`
- `Làm Phase 3 trong docs/ai-chatbot-gemini-phase-plan.md cho tôi`

---

## 6. Sau Khi Phase 3 Xong, Bạn Điền Gì Vào Đâu?

Hướng đích của branch này là:

- bạn chỉ cần điền `AI__ApiKey`
- có thể chọn thêm `AI__Model` nếu muốn đổi model

Trong hướng Gemini-only merge-ready, bạn không nên cần:

- `AI__Provider`
- `AI__BaseUrl`

trừ khi AI agent báo cáo cuối nói rõ code hiện tại vẫn cần 1 trong 2 field đó.

Vì config sample đã chốt Gemini-only, 2 field trên không cần thiết cho setup thông
thường nữa.

### Cách set local trên Windows PowerShell

```powershell
$env:AI__ApiKey = "YOUR_REAL_GEMINI_API_KEY"
$env:AI__Model = "gemini-2.5-flash-lite"
dotnet run --project .\TCTEnglish\TCTEnglish.csproj
```

Nếu bạn muốn giữ model mặc định trong code thì có thể chỉ set:

```powershell
$env:AI__ApiKey = "YOUR_REAL_GEMINI_API_KEY"
dotnet run --project .\TCTEnglish\TCTEnglish.csproj
```

### Cách set trên host khi deploy

Thêm environment variables trong hosting platform:

```text
AI__ApiKey=YOUR_REAL_GEMINI_API_KEY
AI__Model=gemini-2.5-flash-lite
```

Nếu code cuối cùng của agent đã mặc định model trong repo thì `AI__Model` có thể bỏ
qua.

---

## 7. Model Nên Dùng Lúc Đầu

Khuyến nghị bắt đầu:

- `gemini-2.5-flash-lite`

Khi nào mới đổi sang model khác:

- chỉ đổi nếu bạn đã test thật và thấy chat trả lời chưa đủ tốt

Khi cần nâng cấp nhẹ:

- `gemini-2.5-flash`

Không nên đổi sang preview model chỉ vì thấy “mới hơn”, vì mục tiêu branch này là:

- dễ merge
- dễ demo nhỏ
- dễ ổn định

---

## 8. Nếu Gặp Giới Hạn Free Tier Thì Phải Hiểu Đúng

Nếu bạn bị `429`, điều đó thường có nghĩa là bạn đã chạm 1 trong các limit:

1. RPM
2. TPM
3. RPD

Tài liệu chính thức hiện tại nói rõ:

- rate limits là theo `project`
- `RPD` reset vào `midnight Pacific time`

Nguồn:

- [ai.google.dev/gemini-api/docs/rate-limits](https://ai.google.dev/gemini-api/docs/rate-limits)

Hướng xử lý đúng:

- đừng spam API thật để “test cho chắc”
- chờ quota reset nếu cần
- test logic `429` bằng fake/stub provider trong test, không test bằng cách đốt quota
  thật

---

## 9. Bạn Không Nên Làm Gì

Không nên:

1. dán key vào `appsettings.json`
2. dán key vào `appsettings.Development.json`
3. commit key thật
4. gọi Gemini trực tiếp từ browser với key thật
5. nghĩ rằng tạo thêm key trong cùng project sẽ có thêm free quota

Key phải đi từ backend của app.

---

## 10. Checklist Sau Khi Điền Key

Sau khi bạn đã set key local hoặc trên host, hãy test:

1. Đăng nhập
2. Mở `/AI/Chat`
3. Gửi câu đầu tiên
4. Xác nhận conversation được tạo
5. Gửi tiếp câu thứ hai
6. Mở lại history
7. Test launcher embedded
8. Test câu trả lời dài có markdown

Bạn cũng nên match với `Phase 4` và `Phase 5`:

1. `/AI/Chat` mở được sau login
2. First send khi chưa có `conversationId` -> tạo conversation mới
3. History switching giữa 2 conversation
4. Launcher embedded mở/đóng bình thường
5. `409` khi gửi 2 request đồng thời cùng conversation
6. `429` được xử lý đúng
7. `503` được xử lý đúng
8. Markdown dài render đúng

Gợi ý đúng:

- `409`, `429`, `503` nên test bằng integration/fake provider
- không nên dùng quota thật để ép free tier báo lỗi

---

## 11. Sau Khi Gemini Chạy Ổn, Bạn Bảo AI Agent Làm Gì?

Sau khi bạn đã set key và confirm local smoke test ổn:

1. `Làm Phase 4 trong docs/ai-chatbot-gemini-phase-plan.md cho tôi`
2. `Làm Phase 5 trong docs/ai-chatbot-gemini-phase-plan.md cho tôi`

Mục tiêu:

- chạy full test
- fix regression nếu có
- dọn branch sạch
- xác nhận merge readiness

---

## 12. Checklist Kết Thúc Nhanh Và Merge

Trước khi merge vào `master`, bạn cần xác nhận:

- [ ] API key không nằm trong repo
- [ ] AI agent đã làm xong tối thiểu đến `Phase 5`
- [ ] `dotnet test TCTEnglish.Tests -c Release --no-restore` pass
- [ ] chat hoạt động với Gemini
- [ ] docs cuối cùng phản ánh đúng hướng `Gemini-only`
- [ ] không còn file tạm, file rác, hoặc config localhost sai hướng

Command gợi ý:

```powershell
dotnet test .\TCTEnglish.Tests\TCTEnglish.Tests.csproj -c Release --no-restore
git status --short
git diff --name-only
```

Nếu muốn quét nhanh chuỗi nhạy cảm trong file đã đổi:

```powershell
git diff --name-only | ForEach-Object { Select-String -Path $_ -Pattern 'AIza|api[_-]?key|client[_-]?secret|password' -SimpleMatch -ErrorAction SilentlyContinue }
```

---

## 13. Thứ Tự Ngắn Gọn Để Bạn Nhớ

1. Bảo agent làm tới `Phase 3`
2. Vào AI Studio tạo Gemini API key
3. Set:
   - `AI__ApiKey=<key thật>`
   - `AI__Model=gemini-2.5-flash-lite`
4. Chạy app và test local
5. Bảo agent làm `Phase 4`
6. Bảo agent làm `Phase 5`
7. Kiểm tra secrets
8. Merge branch

---

## 14. Nếu Sau Này Bạn Muốn Nâng Cấp

Nếu website có nhiều người dùng hơn hoặc cần privacy tốt hơn, bạn có thể:

1. nâng lên paid tier
2. giữ nguyên hướng code Gemini-only
3. không cần viết lại tính năng từ đầu

Đó là lý do hướng `Gemini free tier -> paid tier` là con đường phù hợp nhất cho
nhánh này.
