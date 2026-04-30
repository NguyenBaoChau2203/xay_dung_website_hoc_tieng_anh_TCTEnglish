# Payment Go-Live Plan cho dự án TCTEnglish

> File này được viết để đưa cho Codex/AI coding agent tiếp tục sửa code.  
> Ngôn ngữ ưu tiên trong issue/commit: tiếng Việt hoặc tiếng Anh đều được, nhưng code/comment kỹ thuật nên thống nhất theo style hiện tại của project.

---

## 0. Bối cảnh review

Dự án: **xây dựng website học từ vựng tiếng Anh - TCTEnglish**  
Thư mục chính trong archive: `xay_dung_website_hoc_tu_vung_tieng_anh_TCT/TCTEnglish`

Phạm vi đã đọc/review tĩnh:

- Billing domain models
- Payment orders/events/subscriptions
- VNPay gateway/signature/IPN/return
- MoMo gateway/signature/IPN/return
- Premium page/controller/view model
- Admin billing management
- Workers liên quan payment/subscription
- Migration billing
- Test files liên quan billing/payment/premium
- Config/docs liên quan payment/secret

**Lưu ý quan trọng:** review này là **static code review** trên archive. Chưa xác nhận runtime bằng `dotnet build`/`dotnet test` trong môi trường review ban đầu. Codex cần chạy build/test trong môi trường có đúng .NET SDK.

---

## 1. Đánh giá nhanh trạng thái thanh toán

### Kết luận

Phần nền thanh toán đã làm khá nhiều, ước khoảng **75–80% về mặt code nền**, nhưng **chưa đủ điều kiện go-live nhận tiền thật**.

Các thành phần đã có:

- Bảng `PremiumPlans`
- Bảng `PaymentOrders`
- Bảng `PaymentEvents`
- Bảng `UserSubscriptions`
- Bảng `PaymentAdminActions`
- `BillingService`
- `IpnService`
- VNPay gateway
- MoMo gateway
- Return URL handlers
- IPN endpoints
- Admin billing dashboard
- Một số workers
- Nhiều unit/integration test liên quan billing/payment

Các blocker chính:

1. Secret thật có dấu hiệu đang bị commit trong `appsettings.json`/docs.
2. `BillingSeedData.SeedAsync` đã có nhưng chưa được gọi trong startup.
3. MoMo IPN/return signature có nguy cơ sai theo tài liệu chính thức.
4. VNPay signature cần verify/fix encoding.
5. Worker dọn pending và hết hạn premium tồn tại nhưng chưa được đăng ký đủ.
6. Reconciliation VNPay đang là stub/no-op.
7. Manual review/refund/admin resolve chưa đủ cho vận hành production.
8. UI/status chưa bao phủ hết các trạng thái như `manual_review`, `partially_refunded`.

---

## 2. Nguồn tài liệu payment chính thức cần bám theo

Codex nên ưu tiên tài liệu official:

- VNPay payment/Return/IPN: `https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html`
- VNPay QueryDR/Refund: `https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html`
- MoMo one-time wallet payment: `https://developers.momo.vn/v3/docs/payment/api/wallet/onetime/`
- MoMo payment notification/IPN: `https://developers.momo.vn/v3/docs/payment/api/result-handling/notification/`
- MoMo ATM one-time docs có ví dụ callback signature field order: `https://developers.momo.vn/v3/docs/payment/api/atm/onetime/`

---

## 3. File/thành phần đáng chú ý trong project

### Billing/payment core

- `TCTEnglish/Services/Billing/BillingService.cs`
- `TCTEnglish/Services/Billing/IpnService.cs`
- `TCTEnglish/Services/Billing/SubscriptionService.cs`
- `TCTEnglish/Services/Billing/PaymentProviderHealthService.cs`
- `TCTEnglish/Services/Billing/VnPayGateway.cs`
- `TCTEnglish/Services/Billing/MoMoGateway.cs`
- `TCTEnglish/Services/Billing/VnPaySignatureHelper.cs`
- `TCTEnglish/Services/Billing/MoMoSignatureHelper.cs`
- `TCTEnglish/Services/Billing/NoOpVnPayQueryClient.cs`
- `TCTEnglish/Services/Billing/IVnPayQueryClient.cs`

### Models/constants/options

- `TCTEnglish/Models/BillingSeedData.cs`
- `TCTEnglish/Models/PaymentOrder.cs`
- `TCTEnglish/Models/PaymentEvent.cs`
- `TCTEnglish/Models/UserSubscription.cs`
- `TCTEnglish/Models/PremiumPlan.cs`
- `TCTEnglish/Models/PaymentAdminAction.cs`
- `TCTEnglish/Models/BillingConstants.cs`
- `TCTEnglish/Models/VnPayOptions.cs`
- `TCTEnglish/Models/MoMoOptions.cs`

### Controllers/views

- `TCTEnglish/Controllers/PremiumController.cs`
- `TCTEnglish/Controllers/BillingController.cs`
- `TCTEnglish/Controllers/Api/BillingIpnController.cs`
- `TCTEnglish/Areas/Admin/Controllers/BillingManagementController.cs`
- `TCTEnglish/Views/Premium/*`
- `TCTEnglish/Views/Billing/*`
- `TCTEnglish/Areas/Admin/Views/BillingManagement/*`

### Workers

- `TCTEnglish/Workers/PaymentReconciliationWorker.cs`
- `TCTEnglish/Workers/PendingPaymentCleanupWorker.cs`
- `TCTEnglish/Workers/PremiumExpiryWorker.cs`

### Startup/config

- `TCTEnglish/Program.cs`
- `TCTEnglish/appsettings.json`
- `TCTEnglish/appsettings.Development.json`
- `TCTEnglish/TCTEnglish.csproj`

### Migrations

- `TCTEnglish/Migrations/20260425152255_AddBillingAndSubscriptions.cs`
- `TCTEnglish/Migrations/20260426054141_PaymentCoreHardening.cs`
- `TCTEnglish/Migrations/20260426072939_AddPaymentAdminAuditLog.cs`
- `TCTEnglish/Migrations/20260427050443_AddMoMoCheckoutFields.cs`

### Docs

- `docs/payment-configuration.md`
- `SECRETS.md`

### Tests liên quan

Tìm nhanh:

```bash
find . -type f \( -name "*Payment*Tests.cs" -o -name "*Billing*Tests.cs" -o -name "*MoMo*Tests.cs" -o -name "*VnPay*Tests.cs" -o -name "*Premium*Tests.cs" -o -name "*Subscription*Tests.cs" \)
```

---

## 4. P0/P1/P2 blocker table

| Priority | Vấn đề | Ảnh hưởng | Hướng xử lý |
|---|---|---|---|
| P0 | Secret thật có dấu hiệu bị commit | Không được go-live; rủi ro lộ DB/API/OAuth/SMTP | Xóa secret khỏi repo, rotate toàn bộ, dùng env vars/user-secrets/vault |
| P0 | `BillingSeedData.SeedAsync` chưa được gọi | Fresh DB không có gói Premium; checkout có thể fail `PLAN_NOT_FOUND` | Gọi seed billing trong `Program.cs` sau migrate DB |
| P0 | MoMo callback/IPN signature có nguy cơ sai | MoMo payment thật có thể không activate premium | Fix raw signature theo field order official, inject `accessKey` |
| P0/P1 | VNPay signature encoding chưa chắc đúng | Có thể invalid signature với ký tự đặc biệt/tiếng Việt/URL | Fix/verify helper bằng official sample + sandbox |
| P1 | VNPay config không bắt buộc `IpnUrl` | Provider có thể health green nhưng không activate vì không có IPN | Bắt buộc `IpnUrl` khi enabled |
| P1 | `PendingPaymentCleanupWorker` và `PremiumExpiryWorker` chưa đăng ký hosted service | Pending order/subscription hết hạn không tự xử lý | Đăng ký worker trong `Program.cs`, thêm config enabled |
| P1 | Reconciliation VNPay đang no-op | IPN mất/chậm không tự khôi phục | Implement QueryDR thật hoặc disable/ghi rõ manual SOP |
| P1 | Manual review/refund chưa đủ flow | Đơn bất thường bị treo; vận hành phải sửa DB tay | Thêm resolve manual review, audit, refund flow |
| P2 | UI/status chưa bao phủ đủ | Admin/user hiểu sai trạng thái | Thêm `manual_review`, `partially_refunded` vào view/filter/badge |
| P2 | Docs chưa đủ MoMo production | Dễ cấu hình sai khi deploy | Cập nhật docs/runbook/checklist |

---

# 5. Kế hoạch triển khai chi tiết cho Codex

## Ticket 01 - P0 - Xóa secret khỏi repo và harden config

### Mục tiêu

Không còn secret thật trong source code/config/docs. App phải lấy secret từ environment variables, user-secrets hoặc secret manager.

### File cần kiểm tra/sửa

- `TCTEnglish/appsettings.json`
- `TCTEnglish/appsettings.Development.json`
- `SECRETS.md`
- `.gitignore`
- README/docs nếu có nhắc secret
- CI config nếu có

### Việc cần làm

1. Xóa mọi giá trị thật khỏi `appsettings.json`.
2. Chỉ giữ placeholder an toàn hoặc non-secret config.
3. Không commit:
   - database password
   - OAuth client secret
   - SMTP password/app password
   - AI API key
   - Pixabay API key
   - VNPay/MoMo secret
4. Dùng environment variables cho production.
5. Dùng `.NET user-secrets` cho local dev.
6. Cập nhật docs hướng dẫn set secret.

### Gợi ý config placeholder

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "VNPay": {
    "Enabled": false,
    "TmnCode": "",
    "HashSecret": "",
    "BaseUrl": "",
    "ReturnUrl": "",
    "IpnUrl": ""
  },
  "MoMo": {
    "Enabled": false,
    "PartnerCode": "",
    "AccessKey": "",
    "SecretKey": "",
    "BaseUrl": "",
    "CreatePath": "/v2/gateway/api/create",
    "RedirectUrl": "",
    "IpnUrl": "",
    "RequestType": "captureWallet",
    "Lang": "vi",
    "MockModeEnabled": false
  }
}
```

### Acceptance criteria

- Không còn literal secret trong repo.
- `appsettings.json` safe để commit public.
- App local chạy bằng `dotnet user-secrets` hoặc env vars.
- Docs có hướng dẫn set env vars.

### Validation commands

```bash
grep -RInE "password|secret|clientsecret|apikey|api_key|smtp|hashsecret|accesskey|private|token" \
  --exclude-dir bin --exclude-dir obj --exclude-dir .git --exclude-dir .vs .
```

> Sau khi xóa khỏi working tree vẫn phải rotate secret đã từng bị commit. Nếu repo đã push remote/public, cần xử lý git history bằng quy trình phù hợp.

---

## Ticket 02 - P0 - Gọi seed Premium plans khi startup

### Mục tiêu

Fresh database sau migrate phải tự có plan premium monthly/yearly.

### File cần sửa

- `TCTEnglish/Program.cs`
- Có thể kiểm tra thêm `TCTEnglish/Models/BillingSeedData.cs`

### Vấn đề hiện tại

`BillingSeedData.SeedAsync` đã tồn tại nhưng startup chưa gọi. Hiện `Program.cs` chỉ thấy seed vocabulary/listening lesson.

### Việc cần làm

Trong `Program.cs`, sau khi migrate DB và seed dữ liệu khác, thêm:

```csharp
try
{
    await TCTEnglish.Models.BillingSeedData.SeedAsync(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "BillingSeedData: lỗi khi seed gói Premium.");
}
```

### Acceptance criteria

- `premium_monthly` tồn tại.
- `premium_yearly` tồn tại.
- Seed idempotent: chạy nhiều lần không duplicate.
- `/Premium` hiển thị plan sau fresh DB.

### Validation commands

```bash
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test -c Release --filter "Billing|Premium|Subscription"
```

SQL verify:

```sql
SELECT Code, Name, PriceVnd, DurationDays, IsActive
FROM PremiumPlans
ORDER BY DisplayOrder;
```

---

## Ticket 03 - P0 - Build/test baseline

### Mục tiêu

Biết code hiện tại compile/test pass trên SDK thật.

### File cần kiểm tra

- `global.json` nếu có
- `TCTEnglish/TCTEnglish.csproj`
- solution `.sln`
- test project `.csproj`

### Việc cần làm

1. Kiểm tra target framework:
   - Project đang có dấu hiệu target `.NET 10`.
   - Production host phải có runtime/SDK tương ứng.
2. Chạy restore/build/test.
3. Fix compile errors trước khi sửa feature sâu.
4. Bật CI nếu chưa có.

### Commands

```bash
dotnet --info
dotnet restore xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln
dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --no-restore
dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --no-build
```

Nếu solution path khác, dùng:

```bash
find . -name "*.sln" -o -name "*.csproj"
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

### Acceptance criteria

- `dotnet build -c Release` pass.
- `dotnet test -c Release` pass.
- Nếu chưa pass, tạo commit fix build trước khi làm ticket khác.

---

## Ticket 04 - P0 - Fix MoMo IPN/Return signature

### Mục tiêu

MoMo IPN/return verify signature đúng theo official docs để payment thật activate được.

### File cần sửa

- `TCTEnglish/Services/Billing/MoMoSignatureHelper.cs`
- `TCTEnglish/Services/Billing/MoMoGateway.cs`
- `TCTEnglish/Services/Billing/IpnService.cs`
- `TCTEnglish/Controllers/Api/BillingIpnController.cs`
- Tests:
  - `*MoMoSignatureHelperTests.cs`
  - `*MoMoIpnServiceTests.cs`
  - `*MoMoGatewayTests.cs`

### Vấn đề nghi ngờ hiện tại

Code hiện tại có dấu hiệu build signature IPN/return bằng cách sort toàn bộ inbound params và không inject `accessKey`. Tài liệu MoMo cho payment notification dùng recipe fixed fields.

### Raw signature callback/IPN nên theo dạng

```text
accessKey={accessKey}
&amount={amount}
&extraData={extraData}
&message={message}
&orderId={orderId}
&orderInfo={orderInfo}
&orderType={orderType}
&partnerCode={partnerCode}
&payType={payType}
&requestId={requestId}
&responseTime={responseTime}
&resultCode={resultCode}
&transId={transId}
```

Khi implement thực tế cần dùng đúng format không xuống dòng:

```text
accessKey=...&amount=...&extraData=...&message=...&orderId=...&orderInfo=...&orderType=...&partnerCode=...&payType=...&requestId=...&responseTime=...&resultCode=...&transId=...
```

### Việc cần làm

1. Tách rõ signature helper:
   - create request signature
   - payment notification/IPN signature
   - return signature nếu cùng format thì dùng chung notification helper
2. Với IPN/return:
   - không sort toàn bộ param tùy tiện nếu docs yêu cầu fixed recipe
   - không đưa unknown fields vào hash
   - lấy `accessKey` từ `MoMoOptions`
   - field missing cần xử lý rõ: verify fail hoặc dùng empty string theo docs nếu optional
3. Verify HMAC-SHA256 bằng `SecretKey`.
4. Không log secret/raw signature chứa secret.
5. Sửa mock payload để giống MoMo thật:
   - `partnerCode`
   - `orderId`
   - `requestId`
   - `amount`
   - `orderInfo`
   - `orderType`
   - `transId`
   - `resultCode`
   - `message`
   - `payType`
   - `responseTime`
   - `extraData`
   - `signature`

### Suggested helper API

```csharp
public static string BuildCreateRawSignature(MoMoCreateSignatureInput input);

public static string BuildPaymentNotificationRawSignature(
    IReadOnlyDictionary<string, string?> fields,
    string accessKey);

public static bool VerifyPaymentNotificationSignature(
    IReadOnlyDictionary<string, string?> fields,
    string accessKey,
    string secretKey);
```

### Tests cần thêm

- Verify official sample payload nếu có.
- Success IPN `resultCode = 0`.
- Failed IPN `resultCode != 0`.
- Invalid signature -> reject.
- Amount mismatch -> reject/manual flow.
- Duplicate IPN -> idempotent.
- Extra unknown field không làm signature fail nếu không thuộc recipe.
- Missing required field -> reject rõ ràng.

### Acceptance criteria

- Unit tests MoMo signature pass.
- MoMo sandbox create checkout pass.
- MoMo sandbox IPN success activate subscription.
- MoMo failed/cancelled không activate subscription.
- Invalid signature không update order paid.

### Validation commands

```bash
dotnet test -c Release --filter "MoMo"
```

---

## Ticket 05 - P0/P1 - Fix/verify VNPay signature encoding

### Mục tiêu

VNPay payment URL, Return URL, IPN verify signature đúng với official docs, kể cả giá trị có dấu cách/tiếng Việt/ký tự đặc biệt.

### File cần sửa

- `TCTEnglish/Services/Billing/VnPaySignatureHelper.cs`
- `TCTEnglish/Services/Billing/VnPayGateway.cs`
- `TCTEnglish/Services/Billing/IpnService.cs`
- Tests:
  - `*VnPayGatewayTests.cs`
  - `*VnPaySignatureHelperTests.cs`
  - `*IpnServiceTests.cs`

### Vấn đề nghi ngờ hiện tại

Comment/implementation có thể lệch nhau. Official VNPay sample build hash data bằng key/value đã URL encode khi tạo secure hash.

### Việc cần làm

1. Kiểm tra helper hiện tại:
   - sort params theo key
   - exclude `vnp_SecureHash`
   - exclude `vnp_SecureHashType`
   - handle empty/null đúng
   - encode key/value đúng theo official sample
2. Dùng cùng logic nhất quán cho:
   - create payment URL
   - return verify
   - IPN verify
3. Thêm test với:
   - `vnp_OrderInfo` có dấu cách
   - tiếng Việt
   - URL
   - ký tự `:`, `/`, `?`, `&`, `=`
4. Confirm với sandbox.

### Acceptance criteria

- Signature helper có test bao phủ encoding.
- VNPay sandbox success.
- VNPay Return URL hiển thị đúng.
- VNPay IPN success activate subscription.
- Invalid signature trả đúng response theo docs.
- Amount mismatch trả đúng response theo docs.

### Validation commands

```bash
dotnet test -c Release --filter "VnPay|Ipn"
```

---

## Ticket 06 - P1 - Bắt buộc `IpnUrl` trong VNPay/MoMo provider health

### Mục tiêu

Không cho provider báo configured/healthy khi thiếu IPN URL.

### File cần sửa

- `TCTEnglish/Models/VnPayOptions.cs`
- `TCTEnglish/Models/MoMoOptions.cs`
- `TCTEnglish/Services/Billing/PaymentProviderHealthService.cs`
- `TCTEnglish/Areas/Admin/Controllers/BillingManagementController.cs`
- View health admin nếu có

### Việc cần làm

1. `VNPay:Enabled=true` thì bắt buộc:
   - `TmnCode`
   - `HashSecret`
   - `BaseUrl`
   - `ReturnUrl`
   - `IpnUrl`
2. `MoMo:Enabled=true` thì bắt buộc:
   - `PartnerCode`
   - `AccessKey`
   - `SecretKey`
   - `BaseUrl`
   - `CreatePath`
   - `RedirectUrl`
   - `IpnUrl`
3. Validate absolute HTTPS URL cho production.
4. Admin health hiển thị rõ missing field.
5. Checkout phải fail fast nếu provider missing required config.

### Acceptance criteria

- Thiếu `IpnUrl` -> health đỏ.
- Thiếu `IpnUrl` -> checkout bị chặn.
- Health dashboard phân biệt enabled/configured/ready.
- Tests pass cho missing config.

### Tests cần thêm

```csharp
[Fact]
public void VnPayOptions_WhenEnabledAndMissingIpnUrl_ReturnsConfigurationError() { }

[Fact]
public void MoMoOptions_WhenEnabledAndMissingIpnUrl_ReturnsConfigurationError() { }
```

---

## Ticket 07 - P1 - Đăng ký worker nền

### Mục tiêu

Pending payment quá hạn và subscription hết hạn được xử lý tự động.

### File cần sửa

- `TCTEnglish/Program.cs`
- `TCTEnglish/Workers/PendingPaymentCleanupWorker.cs`
- `TCTEnglish/Workers/PremiumExpiryWorker.cs`
- `TCTEnglish/Models/BillingOptions.cs` nếu có
- tests liên quan worker nếu có

### Vấn đề hiện tại

Có worker nhưng `Program.cs` chỉ thấy đăng ký `PaymentReconciliationWorker`, `AutoUnlockWorker`, `NotificationWorker`. Chưa thấy đăng ký:

- `PendingPaymentCleanupWorker`
- `PremiumExpiryWorker`

### Việc cần làm

Thêm vào DI:

```csharp
builder.Services.AddHostedService<TCTEnglish.Workers.PendingPaymentCleanupWorker>();
builder.Services.AddHostedService<TCTEnglish.Workers.PremiumExpiryWorker>();
```

Nên có config enable/disable:

```json
{
  "Billing": {
    "PendingPaymentCleanupWorkerEnabled": true,
    "PremiumExpiryWorkerEnabled": true
  }
}
```

Worker nên tự no-op nếu disabled.

### Acceptance criteria

- Worker được đăng ký.
- Pending order hết hạn chuyển `expired`.
- Subscription hết hạn chuyển `expired`.
- User không còn premium nếu hết subscription active.
- Admin role không bị downgrade sai.
- Worker log lỗi nhưng không crash app.

### Validation commands

```bash
dotnet test -c Release --filter "Worker|Pending|Expiry|Subscription"
```

---

## Ticket 08 - P1 - Implement VNPay QueryDR reconciliation thật

### Mục tiêu

Nếu IPN bị mất/chậm, hệ thống có thể query provider để đối soát.

### File cần sửa/tạo

- `TCTEnglish/Services/Billing/IVnPayQueryClient.cs`
- `TCTEnglish/Services/Billing/NoOpVnPayQueryClient.cs`
- Tạo `TCTEnglish/Services/Billing/VnPayQueryClient.cs`
- `TCTEnglish/Workers/PaymentReconciliationWorker.cs`
- `TCTEnglish/Models/VnPayOptions.cs`
- tests reconciliation/query client

### Vấn đề hiện tại

`Program.cs` có dấu hiệu đăng ký `IVnPayQueryClient` bằng `NoOpVnPayQueryClient`. Đây chưa đủ production.

### Việc cần làm

1. Implement HTTP client thật cho VNPay QueryDR.
2. Dùng endpoint QueryDR/Refund official.
3. Build checksum theo rule QueryDR, không dùng nhầm payment URL signature.
4. Timeout/retry hợp lý.
5. Lưu response provider vào `PaymentEvents` loại `reconcile`.
6. Nếu query trả paid:
   - mark order paid
   - activate subscription
   - idempotent
7. Nếu failed/cancelled:
   - update status
8. Nếu ambiguous/error:
   - giữ pending hoặc chuyển `manual_review` tùy rule.
9. Thêm config:
   - reconciliation enabled/disabled
   - interval
   - max age
   - max attempts

### Acceptance criteria

- Không còn dùng NoOp khi production enabled.
- Reconciliation query được pending orders.
- Paid provider result activate subscription.
- Failed provider result không activate.
- Provider error không làm mất dữ liệu.
- Có audit/event đầy đủ.

### Validation commands

```bash
dotnet test -c Release --filter "Reconciliation|VnPayQuery|QueryDR"
```

---

## Ticket 09 - P1 - Hoàn thiện manual review flow

### Mục tiêu

Admin xử lý được order bất thường mà không sửa DB tay.

### File cần sửa

- `TCTEnglish/Areas/Admin/Controllers/BillingManagementController.cs`
- `TCTEnglish/Areas/Admin/Views/BillingManagement/*`
- `TCTEnglish/Services/Billing/BillingService.cs`
- `TCTEnglish/Services/Billing/SubscriptionService.cs`
- `TCTEnglish/Models/PaymentAdminAction.cs`
- `TCTEnglish/Models/BillingConstants.cs`

### Việc cần làm

Thêm admin action:

1. `ResolveManualReviewConfirmPaid`
   - yêu cầu reason
   - mark order `paid`
   - activate subscription nếu chưa active
   - ghi `PaymentEvent`
   - ghi `PaymentAdminAction`
2. `ResolveManualReviewReject`
   - yêu cầu reason
   - mark order `failed` hoặc `cancelled`
   - không activate
   - ghi event/audit
3. `RetryReconciliation`
   - gọi query provider nếu có
   - ghi event/audit
4. Thêm filter `manual_review`.
5. Thêm badge/status display.

### Quy tắc an toàn

- Không cho confirm paid nếu order amount/plan thiếu hoặc provider mismatch chưa được admin xác nhận reason.
- Không activate 2 lần.
- Không downgrade/upgrade sai role.
- Mọi thao tác admin phải có reason và audit.

### Acceptance criteria

- Order `manual_review` có thể resolve từ admin UI.
- Mọi resolve tạo `PaymentAdminAction`.
- Subscription chỉ tạo/extend một lần.
- Có tests cho happy path và duplicate action.

### Validation commands

```bash
dotnet test -c Release --filter "Admin|ManualReview|BillingManagement"
```

---

## Ticket 10 - P1 - Refund flow hoặc disable refund UI rõ ràng

### Mục tiêu

Không có nút/constant refund nửa vời gây hiểu nhầm. Nếu support refund thì phải gọi provider API thật; nếu chưa support thì ẩn/disable rõ.

### File cần sửa

- `TCTEnglish/Models/BillingConstants.cs`
- `TCTEnglish/Services/Billing/*Refund*` nếu có/tạo mới
- `TCTEnglish/Areas/Admin/Controllers/BillingManagementController.cs`
- Admin views
- docs/runbook

### Option A - Chưa làm refund thật

1. Ẩn nút refund.
2. Docs ghi rõ: refund xử lý thủ công qua cổng provider, sau đó admin ghi chú/audit nếu cần.
3. Không expose partial refund status nếu chưa dùng.

### Option B - Làm refund thật

1. Tạo interface:
   ```csharp
   public interface IPaymentRefundService
   {
       Task<RefundResult> RefundAsync(long paymentOrderId, long amountVnd, string reason, string adminUserId, CancellationToken cancellationToken);
   }
   ```
2. Implement VNPay refund API.
3. Implement MoMo refund/query refund nếu provider hỗ trợ trong tài khoản merchant.
4. Update order:
   - full refund -> `refunded`
   - partial refund -> `partially_refunded`
5. Ghi `PaymentEvent` và `PaymentAdminAction`.
6. Không revoke premium tự động nếu business chưa định nghĩa; cần rule rõ.

### Acceptance criteria

- Nếu chưa support: UI không có refund action gây hiểu nhầm.
- Nếu support: provider refund API được gọi thật và có audit/test.

---

## Ticket 11 - P2 - Sửa UI/status mapping

### Mục tiêu

User/admin thấy đúng trạng thái payment/subscription.

### File cần sửa

- `TCTEnglish/Services/Billing/BillingService.cs`
- `TCTEnglish/Models/BillingConstants.cs`
- `TCTEnglish/Models/ViewModels/*Payment*`
- `TCTEnglish/Views/Billing/*`
- `TCTEnglish/Areas/Admin/Views/BillingManagement/*`
- `TCTEnglish/Areas/Admin/Controllers/BillingManagementController.cs`

### Việc cần làm

1. `NormalizeStatus` không fallback `manual_review` thành `pending`.
2. Thêm support:
   - `manual_review`
   - `partially_refunded`
   - `refunded`
   - `expired`
3. View/badge/filter có đủ trạng thái.
4. Payment result message:
   - `pending`: đang chờ xác nhận từ cổng thanh toán
   - `paid`: thanh toán thành công, premium đã kích hoạt
   - `failed`: thanh toán thất bại
   - `cancelled`: thanh toán đã hủy
   - `expired`: đơn đã hết hạn
   - `manual_review`: đã ghi nhận bất thường, support sẽ kiểm tra
   - `refunded`: đã hoàn tiền
   - `partially_refunded`: đã hoàn tiền một phần

### Acceptance criteria

- Tất cả status constants render đúng.
- Admin filter dùng được cho `manual_review`.
- Không còn status lạ bị hiển thị thành pending.

---

## Ticket 12 - P2 - Cập nhật docs payment và runbook

### Mục tiêu

Deploy/support không cần đọc code để vận hành payment.

### File cần sửa/tạo

- `docs/payment-configuration.md`
- Tạo `docs/payment-operations-runbook.md`
- Tạo `docs/payment-go-live-checklist.md`

### Nội dung cần có

#### `docs/payment-configuration.md`

- VNPay sandbox config
- VNPay production config
- MoMo sandbox config
- MoMo production config
- Return URL/IPN URL
- Env vars đầy đủ
- Cách test signature
- Cách disable provider khẩn cấp

#### `docs/payment-operations-runbook.md`

Các case:

1. Khách đã bị trừ tiền nhưng chưa có Premium.
2. Order pending quá lâu.
3. Invalid signature tăng bất thường.
4. Duplicate IPN.
5. Amount mismatch.
6. Manual review.
7. Refund.
8. Disable provider khi gateway lỗi.
9. Đối soát cuối ngày.

#### `docs/payment-go-live-checklist.md`

Checklist phase:

- Security
- Build/test
- Migration
- Provider config
- Worker
- Reconciliation
- Sandbox E2E
- Production deploy
- Production smoke test
- Monitoring

### Acceptance criteria

- Docs không chứa secret thật.
- Docs đủ để một dev/admin khác deploy và vận hành.
- Có checklist go-live tick được.

---

# 6. Test matrix bắt buộc

## 6.1 VNPay

- [ ] Create checkout success
- [ ] Provider disabled -> checkout blocked
- [ ] Missing config -> checkout blocked
- [ ] Missing `IpnUrl` -> health đỏ
- [ ] Return URL valid signature -> display result
- [ ] Return URL invalid signature -> reject/display safe error
- [ ] IPN valid success -> order paid + subscription active
- [ ] IPN invalid signature -> response code invalid signature
- [ ] IPN order not found -> response code order not found
- [ ] IPN amount mismatch -> response code amount mismatch
- [ ] IPN duplicate paid -> idempotent
- [ ] IPN failed/cancelled -> no premium
- [ ] Late IPN > allowed window -> manual_review
- [ ] Return before IPN -> no premature activation
- [ ] IPN before Return -> Return displays final paid
- [ ] Encoding test: tiếng Việt/dấu cách/URL/special chars

## 6.2 MoMo

- [ ] Create checkout success
- [ ] Create timeout handled
- [ ] Create non-200 handled
- [ ] Response missing `payUrl`/`deeplink`/`qrCodeUrl` handled
- [ ] IPN success `resultCode=0` -> paid + premium
- [ ] IPN failed `resultCode!=0` -> no premium
- [ ] IPN invalid signature -> reject
- [ ] IPN duplicate -> idempotent
- [ ] Amount mismatch -> reject/manual review
- [ ] Extra unknown fields don't affect signature if not in recipe
- [ ] Missing required signature field -> reject
- [ ] Return URL display only
- [ ] Mock mode disabled outside Development

## 6.3 Subscription

- [ ] New subscription active
- [ ] Existing active subscription extends
- [ ] Expired subscription then new payment works
- [ ] Admin role not downgraded
- [ ] PremiumExpiryWorker downgrades eligible users
- [ ] Paid order cannot activate twice

## 6.4 Admin

- [ ] Grant premium
- [ ] Revoke premium
- [ ] Mark manual review
- [ ] Resolve manual review as paid
- [ ] Resolve manual review as rejected/cancelled
- [ ] Audit log immutable
- [ ] Status filters work
- [ ] Only Admin can access

## 6.5 Security

- [ ] No secret in repo
- [ ] No secret in logs
- [ ] IPN anonymous but signature mandatory
- [ ] Checkout requires authenticated user
- [ ] Anti-forgery on user/admin POST forms
- [ ] User cannot view other user's order
- [ ] Provider payload stored safely and does not contain secret
- [ ] Rate limit checkout pending orders

---

# 7. Staging sandbox E2E checklist

## Setup

- [ ] Public HTTPS staging domain
- [ ] VNPay sandbox credentials
- [ ] MoMo sandbox credentials
- [ ] Correct Return URLs:
  - `/Billing/VnPayReturn`
  - `/Billing/MoMoReturn`
- [ ] Correct IPN URLs:
  - `/api/billing/vnpay/ipn`
  - `/api/billing/momo/ipn`
- [ ] `/Admin/BillingManagement/Health` green for enabled provider
- [ ] Workers enabled
- [ ] Logs/monitoring enabled

## VNPay E2E

- [ ] Standard user opens `/Premium`
- [ ] Select monthly plan
- [ ] Redirect to VNPay
- [ ] Complete sandbox payment
- [ ] VNPay redirects Return URL
- [ ] VNPay sends IPN
- [ ] `PaymentOrders.Status = paid`
- [ ] `PaymentEvents` has checkout + return + ipn + grant premium
- [ ] `UserSubscriptions` active
- [ ] User can access premium feature

## MoMo E2E

- [ ] Standard user opens `/Premium`
- [ ] Select monthly plan
- [ ] Redirect/deeplink/QR generated
- [ ] Complete sandbox payment
- [ ] MoMo redirects Return URL
- [ ] MoMo sends IPN
- [ ] `PaymentOrders.Status = paid`
- [ ] `PaymentEvents` has checkout + return + ipn + grant premium
- [ ] `UserSubscriptions` active
- [ ] User can access premium feature

## Negative cases

- [ ] Cancel payment
- [ ] Failed payment
- [ ] Invalid signature request
- [ ] Duplicate IPN
- [ ] Amount mismatch
- [ ] Pending order expiry
- [ ] Manual review resolve
- [ ] Reconciliation recovers pending order if IPN missing

---

# 8. Production go-live checklist

## Trước deploy

- [ ] Rotate toàn bộ secret từng bị commit.
- [ ] Không còn secret thật trong repo.
- [ ] `dotnet build -c Release` pass.
- [ ] `dotnet test -c Release` pass.
- [ ] DB backup xong.
- [ ] Hosting hỗ trợ đúng .NET runtime.
- [ ] Production env vars đã set.
- [ ] Ban đầu để:
  - `VNPay__Enabled=false`
  - `MoMo__Enabled=false`
- [ ] Migration đã chạy.
- [ ] Premium plans seeded.
- [ ] Admin health dashboard hoạt động.
- [ ] Logs không có secret.
- [ ] DataProtection keys persist nếu multi-instance/restart thường xuyên.

## Deploy

- [ ] Deploy artifact.
- [ ] Run migration.
- [ ] Verify `/Premium`.
- [ ] Verify `/Admin/BillingManagement/Health`.
- [ ] Verify app normal features.
- [ ] Verify support email/contact.

## Bật provider theo từng bước

### VNPay trước

- [ ] Set `VNPay__Enabled=true`
- [ ] Recycle app
- [ ] Health green
- [ ] Production smoke test giao dịch nhỏ
- [ ] IPN success
- [ ] Premium active
- [ ] Monitor 24–48 giờ

### MoMo sau

Chỉ bật khi signature IPN đã fix và sandbox pass.

- [ ] Set `MoMo__Enabled=true`
- [ ] Recycle app
- [ ] Health green
- [ ] Production smoke test giao dịch nhỏ
- [ ] IPN success
- [ ] Premium active
- [ ] Monitor 24–48 giờ

## Rollback/emergency

- [ ] Nếu payment lỗi: set provider enabled false.
- [ ] Không xóa payment orders thật.
- [ ] Dùng manual review/admin audit để xử lý.
- [ ] Nếu migration lỗi nghiêm trọng: restore DB backup.
- [ ] Nếu deploy lỗi app: rollback artifact.

---

# 9. Monitoring cần có sau go-live

Tạo dashboard/alert cho:

- [ ] Pending orders quá 30 phút
- [ ] Paid orders không có subscription active
- [ ] Invalid signature tăng bất thường
- [ ] `manual_review` count > 0
- [ ] Provider checkout fail rate
- [ ] IPN endpoint 5xx
- [ ] Worker exception
- [ ] Revenue/day
- [ ] Refund count
- [ ] Admin grant/revoke actions

Gợi ý alert:

- Pending > 10 đơn trong 1 giờ
- Paid but not activated > 0
- Invalid signature > 5 trong 10 phút
- IPN 5xx > 1
- Worker không chạy > 2 interval

---

# 10. Prompt gợi ý để đưa cho Codex

Dán đoạn này cho Codex trong repo:

```text
Bạn đang làm trong repo TCTEnglish. Hãy đọc file PAYMENT_GO_LIVE_PLAN.md này và thực hiện theo thứ tự ưu tiên.

Yêu cầu:
1. Không commit secret thật.
2. Làm từng ticket nhỏ, mỗi ticket một commit hoặc một PR riêng.
3. Luôn chạy dotnet build và dotnet test sau mỗi nhóm thay đổi.
4. Ưu tiên P0 trước:
   - remove/harden secrets
   - seed billing plans in Program.cs
   - build/test baseline
   - fix MoMo IPN/return signature
   - fix/verify VNPay signature encoding
5. Khi sửa payment signature, bám theo official docs VNPay/MoMo trong section nguồn tài liệu.
6. Không activate Premium từ Return URL; chỉ activate từ IPN hoặc manual admin resolve có audit.
7. Giữ idempotency: duplicate IPN/admin action không được tạo subscription/lợi ích 2 lần.
8. Bổ sung test cho mọi thay đổi payment quan trọng.
9. Cập nhật docs và go-live checklist.

Trước khi sửa, hãy scan các file liên quan:
- TCTEnglish/Program.cs
- TCTEnglish/Services/Billing/*
- TCTEnglish/Models/*Payment*
- TCTEnglish/Models/*Billing*
- TCTEnglish/Controllers/*Billing*
- TCTEnglish/Controllers/PremiumController.cs
- TCTEnglish/Areas/Admin/Controllers/BillingManagementController.cs
- TCTEnglish/Workers/*Payment*
- TCTEnglish/Workers/*Premium*
- tests liên quan Billing/Payment/MoMo/VnPay/Premium/Subscription

Sau mỗi ticket, báo:
- File đã sửa
- Test đã chạy
- Kết quả build/test
- Rủi ro còn lại
```

---

# 11. Thứ tự làm đề xuất

1. **Ticket 01** - Xóa/rotate secret.
2. **Ticket 02** - Seed Premium plans.
3. **Ticket 03** - Build/test baseline.
4. **Ticket 04** - Fix MoMo IPN/Return signature.
5. **Ticket 05** - Fix/verify VNPay signature encoding.
6. **Ticket 06** - Provider health bắt buộc IPN URL.
7. **Ticket 07** - Đăng ký workers.
8. **Ticket 08** - VNPay QueryDR reconciliation.
9. **Ticket 09** - Manual review resolve.
10. **Ticket 10** - Refund hoặc disable refund UI rõ ràng.
11. **Ticket 11** - UI/status mapping.
12. **Ticket 12** - Docs/runbook/checklist.
13. Staging sandbox E2E.
14. Production deploy provider disabled.
15. Bật VNPay production trước.
16. Bật MoMo production sau.
17. Monitoring vận hành chính thức.

---

## 12. Ghi chú kỹ thuật quan trọng

### Không activate Premium từ Return URL

Return URL là browser redirect, không nên là nguồn tin cậy cuối cùng. Chỉ nên hiển thị kết quả tạm thời/chính thức nếu DB đã có trạng thái. Premium nên activate qua:

1. IPN server-to-server hợp lệ.
2. Reconciliation query provider hợp lệ.
3. Admin manual resolve có audit.

### IPN phải idempotent

Các trường hợp sau không được tạo subscription 2 lần:

- Provider gửi duplicate IPN.
- User refresh Return URL.
- Reconciliation chạy sau IPN success.
- Admin bấm resolve lại.

### Amount/currency/provider mismatch phải reject/manual review

Không được activate nếu:

- Sai amount
- Sai currency
- Sai provider
- Sai order code
- Sai signature
- Order đã expired quá lâu mà chưa có rule xử lý

### Không log secret

Không log:

- VNPay `HashSecret`
- MoMo `SecretKey`
- MoMo `AccessKey` nếu coi là sensitive
- OAuth secret
- API keys
- SMTP password
- raw payload nếu chứa thông tin nhạy cảm

### Production config nên fail fast

Nếu provider enabled nhưng thiếu required config, checkout phải fail trước khi tạo payment URL lỗi. Health dashboard phải đỏ rõ ràng.

---

## 13. Định nghĩa Done cuối cùng

Chức năng thanh toán được coi là sẵn sàng go-live khi:

- [ ] Không còn secret thật trong repo.
- [ ] Secret đã từng lộ đã rotate.
- [ ] Build Release pass.
- [ ] Test suite pass.
- [ ] Billing plans được seed tự động.
- [ ] VNPay signature pass unit test + sandbox E2E.
- [ ] MoMo signature pass unit test + sandbox E2E.
- [ ] IPN success activate premium.
- [ ] Return URL không tự activate premium.
- [ ] Duplicate IPN idempotent.
- [ ] Amount mismatch không activate.
- [ ] Worker pending cleanup chạy.
- [ ] Worker premium expiry chạy.
- [ ] Reconciliation có thật hoặc có SOP manual rõ.
- [ ] Manual review resolve có audit.
- [ ] Refund được implement thật hoặc UI/docs ghi rõ chưa hỗ trợ.
- [ ] Admin health dashboard hiển thị đúng.
- [ ] Docs/runbook/checklist hoàn tất.
- [ ] Staging sandbox pass.
- [ ] Production deploy có backup/rollback.
- [ ] Bật provider từng bước và smoke test thành công.
- [ ] Monitoring/alert sẵn sàng.

