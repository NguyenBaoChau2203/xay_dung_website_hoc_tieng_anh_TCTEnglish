# Plan: Payment (Premium) — correctness fixes and a complete learner flow

Save as `docs/superpowers/plans/2026-11-17-billing-hardening-and-redesign.md`.
Execution prompt: `.ai/prompts/claude-code-billing-hardening-and-redesign.md`.

> **Tóm tắt cho Trọng (tiếng Việt).**
> - **Mình đã đọc toàn bộ code thanh toán và tìm thấy 22 vấn đề** (mục 3). Nặng nhất:
>   1. **Trả tiền xong có thể bị đăng xuất.** Khi đơn được xác nhận, hệ thống đổi vai trò thành Premium; bộ kiểm tra phiên đăng nhập (code của Châu) thấy vai trò khác với cookie nên đăng xuất người dùng.
>   2. **Đơn VNPay đã trả tiền có thể kẹt mãi ở "chờ" rồi "hết hạn".** Trang quay về không tự xác nhận; nếu VNPay không gọi được về máy chủ (luôn xảy ra trên máy cá nhân) thì không ai kích hoạt Premium. Phần đối soát đang tắt và chỉ xem đơn "chờ".
>   3. **Đối soát VNPay có thể đánh dấu "thất bại" cho đơn đang thanh toán dở, và không kiểm tra số tiền.**
>   4. **Biểu tượng trên các trang thanh toán không hiện** (dùng bộ icon mà trang người học không tải).
>   5. **Người đã Premium không thấy ngày hết hạn.**
>   6. Nhiều thông báo tiếng Việt không dấu, giờ hiển thị theo giờ máy chủ, câu chữ kỹ thuật ("IPN") lộ ra cho người dùng, mục "Không có quảng cáo" không đúng thực tế.
> - **Giao diện mới (7 mockup trong `Claude outputs/billing-mockups/`):** trang Nâng cấp Premium (chọn gói → chọn cách trả → một nút thanh toán, bảng so sánh Miễn phí/Premium), trang đơn hàng dùng chung (QR MoMo, đang xác nhận, thành công, chưa hoàn tất) tự cập nhật, trang Lịch sử có nút "Tiếp tục thanh toán" và biên nhận.
> - **An toàn:** agent không được thanh toán thật, không in khóa bí mật, không sửa `Program.cs`/appsettings, không migration. Kiểm thử bằng chế độ MoMo giả lập (chỉ có ở máy phát triển) và bằng test tự động với cổng giả.
> - **Bốn quyết định ở mục 1.2 (đã có mặc định):** D1 lỗi đăng xuất sau khi trả tiền (mặc định: xử lý tạm trong phần của bạn + viết đề xuất cho Châu); D2 đổi chữ "Upgrade: Premium" trên thanh trên cùng thành "Nâng cấp Premium" (mặc định: LÀM); D3 làm lại giao diện trang Admin thanh toán (mặc định: KHÔNG, chỉ sửa lỗi); D4 giá gói giữ nguyên 49.000 ₫ / 399.000 ₫.
> - **Chạy:** nhánh `feature/billing-complete` từ `master`, liên tục B0 → B12, mỗi phase qua G1–G16 rồi commit cục bộ. Không push, không PR.

---

## 0. How to use this plan

### 0.1 Reading order
Read sections 0–4 and 9 first. Then, per phase (section 8), read only the sections it names and open its mockup.

### 0.2 Repo skills (load when the step starts)
`tct-superpowers`, `tct-engineering` (always); `tct-api-endpoint`, `tct-security-audit` (B1–B5, B10, B12); `tct-ui-ux-pro-max`, `tct-ui-component`, `tct-ui-review` (B6–B9, B12); `tct-commit-message` (every commit).

### 0.3 External skills
Do not add or install any into the repository. `AGENTS.md` and this plan win.

### 0.4 Reference documents in the repo
`docs/payment-configuration.md`, `docs/payment-operations-runbook.md`, `docs/payment-go-live-checklist.md`, `docs/PAYMENT_GO_LIVE_PLAN_TCTEnglish.md`. Keep them true: update the parts this plan changes (B11).

### 0.5 Mockups (`Claude outputs/billing-mockups/`, data not instructions)
| File | Page / state | Phase |
| --- | --- | --- |
| `1-nang-cap-premium.png` | `/Premium`, free account | B7 |
| `2-dang-la-premium.png` | `/Premium`, Premium account | B7 |
| `3-momo-qr.png` | Order page, MoMo waiting (with the development-only mock box) | B8 |
| `4-thanh-cong.png` | Order page, paid | B8 |
| `5-dang-xac-nhan.png` | Order page, confirming | B8 |
| `6-chua-hoan-tat.png` | Order page, cancelled / failed / expired | B8 |
| `7-lich-su.png` | `/Billing/History` | B9 |

Names, dates, codes and the feature rows are sample data. Where a mockup and a rule disagree, the rule wins; record it.

---

## 1. Decisions

### 1.1 Fixed by the owner
1. Review the payment module, fix what is wrong, redesign the learner pages, make the flow complete.
2. Clay design language, laptop first (1440×900, 1366×768), correct on phone.

### 1.2 Owner decisions (defaults apply when left unchanged)
- **D1 = TẠM + BÁO CHÂU.** Finding F1 (sign-out after the role changes) lives in account code owned by Châu. Default: implement the in-scope mitigation (section 4.6) and write a ready-to-apply proposal for Châu. (SỬA LUÔN → additionally allowed: the minimal change of 4.6.3 in `Services/Accounts/AccountSessionValidator.cs` and the `OnValidatePrincipal` block of `Program.cs`, nothing else in those files.)
- **D2 = LÀM.** Change the header button text "Upgrade: Premium" in `_Layout.cshtml` to "Nâng cấp Premium" (one string, shared file). (KHÔNG → leave.)
- **D3 = KHÔNG.** Admin billing pages get bug fixes and Vietnamese copy only; their visual redesign waits for the Admin batch. (CÓ → not covered by this plan; stop and report.)
- **D4 = GIỮ.** Plans and prices stay as seeded (`premium_monthly` 49.000 ₫ / 30 days, `premium_yearly` 399.000 ₫ / 365 days).

### 1.3 Design decisions made in this plan
| # | Decision | Why |
| --- | --- | --- |
| P1 | One code path marks an order paid and activates Premium: `IPaymentConfirmationService.ConfirmPaidAsync`. IPN, return-triggered query, reconciliation and admin confirmation all call it | Today the logic exists three times with different checks |
| P2 | Order status changes go through `PaymentOrderStateMachine` (pure, unit-tested) | Failure callbacks can currently overwrite `manual_review`/`refunded`; success can flip `refunded` to `paid` |
| P3 | The return URL never activates anything by itself. A valid, successful return triggers one server-to-server provider query; only a verified query result (or the IPN) confirms | Works on localhost and when an IPN is lost, without trusting the browser |
| P4 | One learner page per order: `GET /Billing/Order/{orderCode}` shows waiting, confirming, paid and not-completed states and updates itself | Replaces two pages with different rules; gives login a place to return to |
| P5 | No migration. Query throttling and history use the existing `PaymentEvents` table | Not needed |
| P6 | Local verification uses MoMo mock mode enabled through process environment variables for that run only | No edit to appsettings or User Secrets; no real money |
| P7 | Learner copy never says "IPN", "signature" or "manual review"; times show in Vietnam time | Plain language |
| P8 | Feature claims on the pricing page come from what the code really gates (verified at B0) | The current list includes "Không có quảng cáo" although the app has no ads |
| P9 | New styles in `wwwroot/css/billing.css` under `.tb-page`, using the Clay kit; icons are Font Awesome (already loaded) | The learner layout does not load Bootstrap Icons |

---

## 2. Scope

### 2.1 May add
- `Services/Billing/PaymentOrderStateMachine.cs`, `IPaymentConfirmationService.cs`, `PaymentConfirmationService.cs`, `BillingTime.cs`, `PremiumBenefits.cs` (the verified feature list), `MoMo/MoMoQueryClient.cs` (+ interface, DTOs)
- `Views/Billing/Order.cshtml`, `Views/Billing/_OrderSummary.cshtml`, `_OrderStatusChip.cshtml`, `_PremiumStatus.cshtml`, `_Receipt.cshtml`
- `ViewModels/Billing/OrderPageViewModel.cs` and small additions
- `wwwroot/css/billing.css`, `wwwroot/js/billing-core.js`, `billing-pricing.js`, `billing-order.js`
- A pinned copy of the QR script under the repo's pinned-asset folder if its process allows (see 5.4)
- Tests: `PaymentOrderStateMachineTests.cs`, `PaymentConfirmationServiceTests.cs`, `BillingReturnConfirmationTests.cs`, `MoMoQueryClientTests.cs`, `BillingPagesIntegrationTests.cs`, `scripts/tests/test_billing_core.cjs`

### 2.2 May edit
- `Controllers/BillingController.cs`; `Services/Billing/**` (all files); `ViewModels/Billing/**`; `Views/Billing/**`
- `Workers/PaymentReconciliationWorker.cs`, `PendingPaymentCleanupWorker.cs`, `PremiumExpiryWorker.cs` (behaviour only as specified; options keep their names)
- `Areas/Admin/Controllers/BillingManagementController.cs` and `Areas/Admin/Views/BillingManagement/**` — fixes and copy only (4.8)
- `wwwroot/css/study-kit.css`: add `.tb-page` to the kit scope lists only
- `Views/Shared/_Layout.cshtml`: the one string of D2
- `Program.cs`: **only** DI registration lines for the new services next to the existing billing registrations (lines ~166–189), because new services cannot be used without them. Nothing else in that file. With D1 = SỬA LUÔN also the `OnValidatePrincipal` block.
- Trọng's billing tests when behaviour changes on purpose (record each and why): `BillingControllerTests`, `BillingServiceTests`, `MoMoBillingControllerTests`, `MoMoIpnServiceTests`, `IpnServiceTests`, `IpnServiceStructureTests`, `SubscriptionServiceTests`, `PremiumAccessServiceTests`, `VnPayQueryClientTests`, `AdminBillingManagementTests`, `BillingStatusViewModelTests`
- Docs: the four payment documents, tracker row for billing pages, `.ai/context/known-issues.md`, `bug-fix-log.md`, `project-structure.md`; this plan's section 12

### 2.3 Forbidden
`appsettings*.json`, User Secrets, `.csproj`, migrations, `Models/**` (including `BillingSeedData.cs`, `PaymentOrder.cs`); the rest of `Program.cs`; account/auth code (except D1 = SỬA LUÔN); `LearningApiController`, Goals, AI, Speaking, Writing, Reading, Listening services (their Premium checks stay as they are); `layout.css`, `site.css`, `tct-learning-ui.css`, admin shell CSS, shared partials; other owners' tests; new packages, fonts or CDNs; **any real payment, any call to a live provider endpoint with real credentials, printing or storing any secret or credential**.

### 2.4 Must stay unchanged
- Public callback URLs and their protocols: `GET /api/billing/vnpay/ipn` (JSON `RspCode`/`Message`, same codes), `POST /api/billing/momo/ipn` (204), `GET /Billing/VnPayReturn`, `GET /Billing/MoMoReturn` — these are registered with the providers.
- Signature algorithms and field order (`VnPaySignatureHelper`, `MoMoSignatureHelper`); order code format; amounts; plan data.
- `/Premium`, `/Billing/Checkout`, `/Billing/History`, `/Billing/OrderStatus`, `/Billing/MoMoQr` (may redirect) keep responding.
- Existing data: no order, event or subscription is rewritten except through the new rules acting on new events.
- Premium checks in other modules.

---

## 3. Findings (static review of `master` at `2842ca4`, 2026-11-17; reproduce or disprove each at B0)

Severity: **A** money or access wrong, **B** flow broken or misleading, **C** polish.

| # | Sev | Where | Finding |
| --- | --- | --- | --- |
| F1 | A | `SubscriptionService.ActivateFromPaidOrderAsync` (sets `User.Role = Premium`) + `Services/Accounts/AccountSessionValidator.cs` (rejects the cookie when the role claim differs from the database) + `Program.cs` `OnValidatePrincipal` (signs out) | Right after a payment is confirmed, the buyer's next request is signed out. The return handler then redirects to login with `returnUrl=/Billing/History`. The same happens at expiry, admin grant and revoke. Also, modules that read the role **claim** (`SetController`, `SetController.Personal`, `VocabApiController`, `AiController`, `_AiChatLauncher`) only see Premium after a new login. |
| F2 | A | `BillingService.GetProviderReturnResultAsync`, `PendingPaymentCleanupWorker`, `PaymentReconciliationService` | The return page is display-only and nothing else confirms a paid VNPay order when the IPN does not arrive (always on localhost; possible in production). The cleanup worker turns pending orders into `expired` after 15 minutes; reconciliation is opt-in (`ReconciliationWorker:Enabled` default false) and only looks at `pending`, so a paid order can stay `expired` for ever. MoMo has no query at all. |
| F3 | A | `VnPayQueryClient.QueryOrderAsync` → `PaymentReconciliationService.ReconcileOrderAsync` | Any query answer that is not success or cancel is treated as **failed**: a transaction still in progress or "not found yet" marks the order `failed` five minutes after creation. The query result carries no amount, so `MarkPaidAsync` confirms without comparing the amount. |
| F4 | A | `IpnService.ProcessVnPayIpnAsync` / `ProcessMoMoIpnAsync` | Status guards only check `paid`. A failure callback can overwrite `manual_review`, `refunded`, `partially_refunded`, `expired`; a success callback can turn `refunded` or `manual_review` into `paid`. |
| F5 | A | `BillingManagementController.ResolveManualReviewConfirmPaid` | Sets `paid`, saves, then activates outside a transaction; a failure leaves a paid order without Premium. `ActivatedAtUtc` is never set. No amount or state check beyond `manual_review`. |
| F6 | B | `IpnService.IsUniqueConstraintViolation` | Detects SQL Server codes and SQLite text only. The database is MariaDB ("Duplicate entry", error 1062), so a concurrent duplicate callback throws instead of being ignored: VNPay gets `99`, MoMo gets HTTP 500. `Services/RelationalUniqueConflict.IsUnique` already handles all three. |
| F7 | B | `BillingService.TrySaveCheckoutEventAsync`, `TrySaveReturnEventAsync` | Catch every `DbUpdateException` and leave the failed entity tracked; the next `SaveChanges` in the same request throws again. |
| F8 | B | `IpnService.ProcessSuccessfulPaymentAsync` | The 24-hour late-callback rule (send to manual review) applies to VNPay only; a MoMo success for a long-expired order activates silently. |
| F9 | B | `BillingController.MoMoIpn` | A body that is not JSON throws (`JsonDocument.ParseAsync`) → HTTP 500 and a log full of stack traces. No size limit. |
| F10 | B | `PremiumAccessService.GetAccessSnapshotAsync` | When the role is already Premium the end date is not loaded, so a paying user never sees "Thời hạn đến". |
| F11 | B | `Views/Billing/*`, `ViewModels/Billing/*` | 33 `bi bi-*` icon classes; the learner layout loads Font Awesome only, so none of the icons render. |
| F12 | B | `BillingService.CreateCheckoutAsync`, `Pricing.cshtml` | An admin (permanent access) can buy. Every click creates a new order; an existing live order is never reused; the VNPay payment URL is not stored, so an order cannot be resumed. Two forms and four buttons per plan. |
| F13 | B | `BillingController.RenderPaymentResultForOwnerAsync` | A valid return for a signed-out browser goes to login and then to the history list, losing the order. |
| F14 | B | `PaymentResult.cshtml`, `PaymentResultViewModel` | Learner copy explains "IPN"; status label "Manual review" in English; the pending state does not update itself. |
| F15 | B | `Pricing.cshtml` | Claims not backed by the product ("Không có quảng cáo"; plan text "toàn bộ tính năng Writing & Speaking"). Disabled-gateway buttons say "chưa khả dụng/chưa cấu hình" to learners. |
| F16 | C | `BillingController`, `MoMoGateway`, `BillingManagementController` | Vietnamese messages without diacritics ("Thong tin dang ky khong hop le.", "Cong thanh toan MoMo chua duoc cau hinh…", "Chi co the confirm paid…"). |
| F17 | C | Billing views | `ToLocalTime()` uses the server time zone; on a UTC server every time is seven hours off. |
| F18 | C | `MoMoQr.cshtml` | Loads `qrcodejs` from a CDN without integrity; styles and script inline. |
| F19 | C | `BillingController.GetClientIp` | Uses the first `X-Forwarded-For` value unchecked as `vnp_IpAddr`; IPv6 loopback (`::1`) is sent on localhost. |
| F20 | C | `BillingService.GetPaymentHistoryAsync`, `History.cshtml` | No paging, no way to continue or inspect an order. |
| F21 | C | `PremiumExpiryWorker` (hourly) | Billing pages can show Premium up to an hour after the end date. |
| F22 | C | `Views/Shared/_Layout.cshtml` line ~68 | Header button text "Upgrade: Premium" in English (D2). |

Checked and found correct: antiforgery on every POST except the provider webhooks (by design); owner checks on order pages; HMAC-SHA512 / HMAC-SHA256 signing and the VNPay IPN response codes; amount check in both IPN handlers; idempotent activation per order; subscription stacking on the current end date; mock confirmation restricted to Development.

---

## 4. Backend specification

### 4.1 `PaymentOrderStateMachine` (pure)
```csharp
public enum PaymentTransitionSource { ProviderCallback, ProviderQuery, Cleanup, Admin }
public static class PaymentOrderStateMachine
{
    public static bool CanMarkPaid(string current, PaymentTransitionSource source);
    public static bool CanMarkFailedOrCancelled(string current, PaymentTransitionSource source);
    public static bool CanExpire(string current);
    public static bool CanSendToManualReview(string current);
    public static bool IsTerminal(string current);
}
```
| From | → paid | → failed / cancelled | → expired | → manual_review |
| --- | --- | --- | --- | --- |
| pending | provider, query, — | provider, query | cleanup | provider, query, admin |
| expired | provider, query (see 4.2 late rule) | never (stays expired, event `ignored`) | — | provider, query, admin |
| failed, cancelled | provider, query (a verified success wins over an earlier failure) | no change | no | admin |
| manual_review | admin only | admin only | no | — |
| paid | — (idempotent) | never | never | never |
| refunded, partially_refunded | never | never | never | never |

Unit tests: every cell.

### 4.2 `IPaymentConfirmationService`
```csharp
public sealed record PaymentConfirmation(
    long OrderId, string Provider, PaymentTransitionSource Source,
    long? VerifiedAmountVnd,            // required unless Source == Admin
    string EventType, string EventKey, string PayloadJson,
    string? ProviderTransactionId, string? ProviderRequestId, string? ResponseCode,
    string? TransactionStatus, string? PayType, string? BankCode, string? BankTranNo, string? CardType,
    string? AdminReason);
public enum PaymentConfirmationOutcome { Activated, AlreadyPaid, SentToManualReview, RejectedState, RejectedAmount, OrderNotFound }
public sealed record PaymentConfirmationResult(PaymentConfirmationOutcome Outcome, DateTime? PremiumEndsAtUtc);
Task<PaymentConfirmationResult> ConfirmPaidAsync(PaymentConfirmation c, CancellationToken ct);
```
Inside one execution-strategy + transaction (as `ProcessSuccessfulPaymentAsync` does today):
1. Clear the change tracker; reload the order with its plan.
2. Already `paid` → `AlreadyPaid` (ensure the subscription exists through the idempotent activation; set `ActivatedAtUtc` if null).
3. `CanMarkPaid` false → save an `ignored` event with the reason → `RejectedState`.
4. `VerifiedAmountVnd` (non-admin) must equal `decimal.Truncate(order.AmountVnd)` → else a `failed` event "Amount mismatch" → `RejectedAmount`; order unchanged.
5. **Late rule, every provider:** the order is `expired` (or `pending` past `ExpiresAtUtc`) and now − `ExpiresAtUtc` > 24 h, source not Admin → `manual_review` + event → `SentToManualReview`.
6. Mark `paid` (all provider fields, `PaidAtUtc`, `RawStatus`, `IpnPayloadJson` for callbacks), save the event (`EventType`/`EventKey` given), activate through `ISubscriptionService.ActivateFromPaidOrderAsync`, set `ActivatedAtUtc`, save the `grant_premium` event (`grant:{OrderCode}`), commit.
7. After commit, best effort and never throwing: create an in-app notification for the buyer through `INotificationService` if it offers a per-user create method (discover at B0): title "Premium đã được kích hoạt", text "Gói {tên gói} có hiệu lực đến {dd/MM/yyyy}." Skip silently when not available; record the choice.
- Unique-conflict handling uses `RelationalUniqueConflict.IsUnique` and detaches the failed entity (F6, F7). Apply the same helper in `IpnService.TrySaveEventAsync` and `BillingService.TrySave*EventAsync`.

`IpnService` success paths, `PaymentReconciliationService.MarkPaidAsync` and the admin confirmation call this service. Their old private copies are removed.

### 4.3 IPN hardening
- Failure callbacks: apply only when `CanMarkFailedOrCancelled`; otherwise save an `ignored` event and acknowledge exactly as today (`00` for VNPay, 204 for MoMo).
- Keep every response code and the order of checks (signature → duplicate → order → provider → amount/currency → result).
- `MoMoIpn` action: read at most 64 KB; invalid JSON or a non-object body → log one warning line (no payload) and return 204 without calling the service; never 500.
- Tests: each cell of 4.1 driven through both handlers; duplicate callback on a provider that reports MariaDB-style and SQLite-style unique errors; malformed MoMo bodies.

### 4.4 Provider queries
**VNPay (`VnPayQueryClient`, `VnPayQueryResult`):**
- Add `AmountVnd` (from `vnp_Amount` ÷ 100), `PayDate`, and a three-way state: `Success` (`vnp_ResponseCode == "00"` **and** `vnp_TransactionStatus == "00"`), `Failed` (`vnp_ResponseCode == "00"` and a final failure status), `Unknown` (everything else: the query itself failed, transaction not found, still processing). Take the status meanings from the VNPay QueryDR documentation referenced in `docs/payment-configuration.md`; if the repo has no table, treat only `00` as success, `02` as failed/cancelled, and all others as `Unknown`, and record that.
- `Unknown` never changes an order.
- Response signature verification stays; the amount must match before confirming.

**MoMo (`MoMoQueryClient`, new):** POST `{BaseUrl}{QueryPath}` with `partnerCode`, `requestId` (new GUID-based), `orderId`, `lang`, `signature` = HMAC-SHA256 over `accessKey=…&orderId=…&partnerCode=…&requestId=…` (confirm the raw-signature format against `MoMoSignatureHelper` conventions and `docs/payment-configuration.md`; if the documents in the repo do not define it, implement behind the interface, keep it disabled unless `IsFullyConfigured()`, and record that it is unverified against a live sandbox). Result: `Success` (`resultCode == 0`, amount returned), `Failed` (final failure codes), `Unknown`. In mock mode the client returns `Unknown` (the mock button is the confirmation path). Timeout from `MoMoOptions.TimeoutSeconds`. Tests with a fake `HttpMessageHandler` only.

**Throttle (no schema):** before a query for an order, read its `PaymentEvents` of type `query`; skip when the newest is younger than 20 seconds or when there are already 12. Each attempt writes one `query` event (`query:{OrderCode}:{ticks}`, result code, safe payload without signatures).

### 4.5 Confirm on return, order refresh, reconciliation
- **Return handlers** (`VnPayReturn`, `MoMoReturn`, still `[AllowAnonymous]`):
  1. Verify the signature and the provider match as today; invalid → the generic invalid page (no order data), unchanged.
  2. Record the return event (as today).
  3. If the provider's return says success and the order is `pending` or `expired`: run one throttled query; a `Success` with a matching amount → `ConfirmPaidAsync(Source = ProviderQuery, EventType = reconcile, EventKey = "retq:{OrderCode}")`.
  4. If the return says cancelled/failed and the order is `pending`: leave the order (the IPN or a later query decides); the page shows "chưa hoàn tất" from the return code while pending.
  5. Signed-in owner → redirect to `/Billing/Order/{orderCode}`. Signed-in non-owner → 404. Anonymous → redirect to login with `returnUrl=/Billing/Order/{orderCode}` (local URL).
- **`POST /Billing/Order/{orderCode}/Refresh`** (`[Authorize]`, antiforgery, owner only): one throttled query for a `pending`/`expired` order younger than 24 h; redirect back (or JSON for `fetch`). Used by "Kiểm tra lại ngay" and by the page's own polling every 15 s for at most 3 minutes after a return.
- **`GET /Billing/OrderStatus`** keeps its fields and adds `stage` (`waiting`, `confirming`, `paid`, `not_completed`), `premiumEndsAt` (ISO) when paid.
- **Reconciliation service:** include `expired` orders whose `ExpiresAtUtc` is within the last 24 h, for both providers; use the three-way result; stop marking `failed` on `Unknown`; keep the 24 h auto-expire and the manual-review rule for orders that stay unknown for more than 1 h after expiry only when the return page recorded a provider success (otherwise they simply stay expired). The worker stays opt-in; document the recommended production setting.
- **Cleanup worker:** unchanged timing; goes through `CanExpire`.

### 4.6 Premium status, expiry and the sign-out problem (F1, F10, F21)
1. **Snapshot:** `GetAccessSnapshotAsync` always loads the latest active end date for non-admins. `IsPremium` keeps today's rule (admin, role Premium, or active subscription). New read-only fields: `IsPermanent` (admin, or role Premium with no subscription rows at all), `DaysLeft`.
2. **Lazy expiry on billing pages:** `/Premium`, `/Billing/History` and the order page call a new `ISubscriptionService.ExpireForUserAsync(userId)` (same rules as `ExpireSubscriptionsAsync`, one user) before building the model.
3. **Sign-out mitigation, in scope (default):**
   - The order page, the pricing summary and the MoMo waiting state tell the buyer once, in plain words: "Sau khi thanh toán xong, bạn có thể được yêu cầu đăng nhập lại để tài khoản nhận quyền Premium."
   - Login always returns to the order page (4.5 step 5). The polling script treats a redirect to the login page or a 401 as "session ended": it stops and navigates to `/Account/Login?returnUrl=/Billing/Order/{code}`.
   - After login the order page shows the paid state with the end date.
   - Write the proposal for Châu into `known-issues.md` (id `BILLING-ROLE-CHANGE-SIGNS-OUT`): in `OnValidatePrincipal`, when everything else is valid and only the role differs, replace the principal with the same claims and the database role and set `ShouldRenew = true` instead of rejecting (the security version check already forces sign-out when needed).
   - **Only with D1 = SỬA LUÔN:** implement exactly that proposal with tests (role upgraded → request stays signed in with the new role claim; security version changed → still rejected; anonymised or locked → still rejected), then remove the warning sentence above.
4. Reproduce F1 at B0 with the mock flow and record what really happens (signed out immediately, or on the next page).

### 4.7 Checkout rules (`BillingService.CreateCheckoutAsync`, `BillingController.Checkout`)
- Admin → fail `ALREADY_PERMANENT`: "Tài khoản quản trị đã có sẵn quyền Premium."
- A buyer who has a `pending`, unexpired order for the same plan and provider created by them: reuse it — MoMo → redirect to its order page; VNPay → redirect to the stored payment URL (store `gatewayResult.RedirectUrl` in `ProviderPaymentUrl` for VNPay from now on; it contains no secret). No new order, no rate-limit hit.
- Otherwise create as today. Keep the 5-orders-per-15-minutes limit; message "Bạn đã tạo quá nhiều đơn thanh toán. Vui lòng thử lại sau vài phút."
- MoMo redirect target becomes `/Billing/Order/{code}`; `/Billing/MoMoQr?orderCode=` redirects there (302) for old links.
- Client IP: prefer `Connection.RemoteIpAddress`; accept the first `X-Forwarded-For` entry only when it parses as an IP address; map IPv6 loopback and IPv4-mapped addresses to IPv4 text; fall back to `127.0.0.1`.
- Every user-facing message in correct Vietnamese (F16), including `MoMoGateway` and `VnPayGateway` failures: "Thông tin đăng ký không hợp lệ.", "Cổng thanh toán hiện không khả dụng.", "Đơn hàng đã hết hạn, vui lòng tạo đơn mới.", "Cổng thanh toán MoMo chưa sẵn sàng.", "Số tiền thanh toán MoMo không hợp lệ.", "Không thể kết nối cổng MoMo lúc này.", "MoMo từ chối tạo phiên thanh toán.", "Phản hồi từ MoMo không hợp lệ.", "MoMo không trả về thông tin thanh toán hợp lệ." A raw provider message is logged, not shown.
- Checkout POST accepts `PlanCode` and `Provider` as today (one form now).

### 4.8 Admin billing (fixes only, D3)
- `ResolveManualReviewConfirmPaid` → `ConfirmPaidAsync(Source = Admin, EventType = manual_confirm, AdminReason)`; audit record kept; on `RejectedState` show the message.
- `ResolveManualReviewReject` and `MarkManualReview` go through the state machine.
- All `TempData` messages in correct Vietnamese; status labels and time display reuse the shared mapping (5.2) and `BillingTime`.
- No layout or style redesign.

### 4.9 Shared display helpers
- `BillingTime.ToDisplay(DateTime utc)` → Vietnam time via the business time zone already used by `BusinessDateHelper`; formats `dd/MM/yyyy HH:mm` and `dd/MM/yyyy`.
- One status mapping for learners (label, tone, Font Awesome icon):

| Status | Label | Tone | Stage |
| --- | --- | --- | --- |
| pending (not expired) | Chờ thanh toán | warning | waiting |
| pending with a successful return, or `manual_review` | Đang xác nhận | warning | confirming |
| paid | Đã thanh toán | success | paid |
| cancelled | Đã hủy | neutral | not_completed |
| failed | Thất bại | danger | not_completed |
| expired | Hết hạn | neutral | not_completed |
| refunded | Đã hoàn tiền | info | not_completed |
| partially_refunded | Hoàn tiền một phần | info | not_completed |

- `PremiumBenefits`: the list of Premium-only features shown on the pages, each verified at B0 against a real gate (file and line recorded in section 12). Candidates from the code: luyện viết với AI (`WritingService.Generation`), thêm video nói riêng và shadowing (`SpeakingService`, `SpeakingImportJobService`), thêm bài nghe từ YouTube, dịch lời thoại và tạo quiz bằng AI cho bài nghe, tốc độ phát 0.75×/1.5× (`ListeningService`), tìm ảnh minh họa khi tạo thẻ (`SetController`, `VocabApiController`), hạn mức trợ lý AI cao hơn (`AiController`). Drop or reword anything that is not gated today. Never list "Không có quảng cáo".

---

## 5. Learner UI specification

Common rules: root `<div class="tct-theme tct-page tb-page">`; Clay kit `k-*` + `billing.css`; tokens only (no hex, `rgb()`, `:root`, `!important`, weight above 700); Font Awesome icons; no inline `<style>`/`<script>` blocks; forms keep antiforgery; focus visible; touch targets ≥ 44 px; forced-colours and reduced-motion blocks; money as `399.000 ₫` (vi-VN grouping, non-breaking space before ₫); status chips never wrap. Vietnamese strings below are literal.

### 5.1 `/Premium` (mockups 1, 2)
- `h1` "Nâng cấp Premium"; sub "Thanh toán một lần cho mỗi gói. Không tự động gia hạn."
- `_PremiumStatus` card:
  - free: "Bạn đang dùng gói Miễn phí" / "Nâng cấp để mở các tính năng dùng AI và nội dung riêng của bạn.";
  - Premium with end date: "Bạn đang dùng Premium" / "Có hiệu lực đến {dd/MM/yyyy} (còn n ngày). Mua thêm sẽ được cộng nối vào hạn này.";
  - permanent (admin or legacy): "Tài khoản của bạn có quyền Premium không thời hạn." and the checkout column is replaced by that note (no pay button);
  - link "Lịch sử thanh toán".
- Error from checkout (`TempData["CheckoutError"]`) as a danger state message above the columns, focused on load.
- **One `<form>`** (POST `/Billing/Checkout`), two columns from 1100 px (main 1fr, summary 380 px sticky), one column below with the summary after the plans:
  - **"1. Chọn gói"** — radio cards (`name="PlanCode"`), one per active plan ordered by `DisplayOrder`: name, price, "Dùng trong n ngày"; for plans of 365 days or more also "khoảng {giá/12} ₫/tháng" and a chip "Tiết kiệm x%" computed against the cheapest monthly plan (hide when not positive). The longest plan is preselected.
  - **"Premium có thêm gì"** — comparison table "Tính năng | Miễn phí | Premium" from `PremiumBenefits` plus two rows that are free for everyone ("Từ vựng, bộ thẻ, luyện tập, ôn theo lịch", "Bài nghe, bài đọc, bài nói có sẵn"). Real `<table>` with `<th scope>`; check and dash icons have text alternatives ("Có", "Không có").
  - **"2. Thanh toán"** — radio rows (`name="Provider"`): "VNPay" / "Thẻ ATM, QR ngân hàng, thẻ quốc tế"; "Ví MoMo" / "Quét mã QR bằng ứng dụng MoMo". Only gateways that are enabled and configured are selectable; a disabled one shows "Tạm thời chưa dùng được". The first available one is preselected. Brand marks are text badges in brand-neutral tokens (no logo files).
  - Summary rows "Gói", "Hiệu lực" ("{n} ngày, đến {dd/MM/yyyy}" — computed from the current end date when extending), "Tổng thanh toán".
  - Submit: "Thanh toán {số tiền}" (lock icon), pending text "Đang chuyển đến cổng thanh toán…", disabled after the first click.
  - Note: "Bạn sẽ được chuyển sang trang của {VNPay|MoMo} để hoàn tất. TCT English không lưu thông tin thẻ của bạn." plus the sentence of 4.6.3.
  - No gateway available: a warning state "Cổng thanh toán đang tạm ngưng. Vui lòng quay lại sau." and no submit button. In Development with MoMo mock mode the MoMo row carries the chip "Thử nghiệm".
- Without JavaScript the form works (radios are native); the summary then shows the preselected plan and the button reads "Thanh toán".
- `billing-pricing.js` only updates the summary and button text from `data-*` attributes; maths in `billing-core.js`.

### 5.2 Order page `GET /Billing/Order/{orderCode}` (mockups 3–6)
Owner only (others 404). Breadcrumb "Nâng cấp Premium / Thanh toán". Summary card on the right (`_OrderSummary`: "Mã đơn", "Gói", "Hiệu lực", "Tổng thanh toán"). Left card by stage:

- **waiting — MoMo:** `h1` "Thanh toán bằng MoMo"; QR (from `ProviderQrCodePayload`; missing → "Không lấy được mã QR. Hãy bấm Mở ứng dụng MoMo hoặc tạo đơn mới."); countdown "Mã hết hạn sau mm:ss" (from `ExpiresAtUtc`, server time offset aware); ordered steps "Mở ứng dụng MoMo trên điện thoại", "Chọn Quét mã và quét mã QR này", "Xác nhận thanh toán {số tiền}"; live line "Đang chờ bạn thanh toán… trang sẽ tự cập nhật"; in the summary "Mở ứng dụng MoMo" (deep link, shown on touch devices; pay URL as a normal link otherwise) and "Hủy và chọn cách khác" → `/Premium`.
  - Development mock only: a dashed box "Chế độ thử nghiệm (chỉ có ở máy phát triển). Không có tiền thật được chuyển." with the existing mock-confirm POST button "Giả lập thanh toán thành công". Never rendered outside Development (server-side check, test).
- **waiting — VNPay** (the buyer came back without paying or opened the link from history): "Đơn đang chờ thanh toán"; button "Tiếp tục thanh toán trên VNPay" (stored URL) while not expired.
- **confirming:** icon, `h1` "Đang xác nhận thanh toán", text "Chúng tôi đang chờ {VNPay|MoMo} xác nhận giao dịch. Việc này thường mất dưới một phút; trang sẽ tự cập nhật.", live line "Đang kiểm tra… lần gần nhất {HH:mm:ss}", button "Kiểm tra lại ngay" (POST refresh), help "Đã bị trừ tiền nhưng quá 15 phút chưa được kích hoạt? Liên hệ hỗ trợ kèm mã đơn ở trên." (link `/Home/Contact`). `manual_review` shows the same page with the text "Đơn đang được kiểm tra thêm. Chúng tôi sẽ cập nhật trong thời gian sớm nhất." and no polling.
- **paid:** `h1` "Thanh toán thành công"; "Premium của bạn có hiệu lực đến {dd/MM/yyyy}."; `_Receipt` ("Mã đơn", "Gói", "Số tiền", "Phương thức" incl. bank code when known, "Thời gian", "Mã giao dịch"); grid of unlocked benefits (from `PremiumBenefits`, max 4); primary "Bắt đầu học" → `/Home/Index`; link "Xem lịch sử thanh toán".
- **not_completed:** `h1` "Thanh toán chưa hoàn tất"; one sentence by status — cancelled "Bạn đã hủy giao dịch trên {cổng}." ; failed "Giao dịch không thành công."; expired "Đơn đã hết hạn trước khi được thanh toán."; refunded "Khoản thanh toán này đã được hoàn lại."; never claim that no money was taken; add "Nếu tài khoản của bạn đã bị trừ tiền, hãy liên hệ hỗ trợ kèm mã đơn." ; primary "Thanh toán lại" → `/Premium` (not for refunded); link to history.
- Invalid return (bad signature, unknown order): the existing generic page, restyled: "Không xác minh được kết quả thanh toán" / "Hãy mở Lịch sử thanh toán để xem trạng thái đơn của bạn." No order data.

**Polling (`billing-order.js` + `billing-core.js`):** only in `waiting` and `confirming`; every 3 s for `waiting` MoMo, every 5 s for `confirming`, backing off to 15 s after one minute; stops at expiry, when the tab is hidden (resumes on return), on a terminal stage, or after 10 minutes; on `paid` it reloads the page once; on 401 or a redirect to login it follows 4.6.3; network errors show "Mất kết nối. Đang thử lại…" without stopping. In `confirming` it also posts the refresh every 15 s for up to 3 minutes. Status text lives in an `aria-live="polite"` region; the countdown is not announced every second.

### 5.3 `/Billing/History` (mockup 7)
- Header "Lịch sử thanh toán" + primary "Mua thêm Premium" (or "Nâng cấp Premium" for free accounts).
- `_PremiumStatus` (compact): "Premium đến {dd/MM/yyyy}" / "Còn n ngày. Kích hoạt từ đơn {mã}." or the free text.
- Orders, newest first, 20 per page (`?page=`): on laptop a table ("Mã đơn", "Gói", "Số tiền", "Phương thức", "Thời gian", "Trạng thái", action); below 768 px stacked cards. Action by stage: waiting and not expired → "Tiếp tục thanh toán"; paid → "Xem biên nhận"; others → "Xem chi tiết" — all link to the order page.
- Empty: "Bạn chưa có giao dịch nào." + "Xem các gói Premium".

### 5.4 QR script
The order page needs `qrcode.min.js`. Follow the repo's pinned-asset process (`wwwroot/lib/pinned` and `scripts/tests/test_pinned_browser_assets.py`): if a new pinned asset is allowed by that process (source, hash, licence recorded), vendor `qrcodejs@1.0.0` there and load it locally; otherwise keep the existing CDN URL and add `integrity` and `crossorigin`. Record which. No other new external request.

### 5.5 Header label (D2)
`_Layout.cshtml`: "Upgrade: Premium" → "Nâng cấp Premium". Nothing else in that file.

### 5.6 Accessibility
Radio groups with `<fieldset>`/`<legend>`; the table has a caption; status chips have text; live regions for status and errors; QR has `alt`-equivalent text "Mã QR thanh toán MoMo cho đơn {mã}"; keyboard-complete; contrast scripted; 200% zoom and 320 px without overlap; reduced motion stops the spinner animation (text stays).

---

## 6. Tests (write first where marked in the phases)
- **State machine:** table of 4.1.
- **Confirmation service:** each outcome; amount mismatch; late rule for both providers; idempotency under a repeated call and under a simulated unique conflict; activation extends an existing subscription; notification failure does not roll back.
- **IPN:** existing tests stay green; new ones for 4.3.
- **Queries:** VNPay three-way mapping, amount, bad signature → `Unknown`; MoMo client with a fake handler (success, failure, timeout, bad JSON); throttle.
- **Return and refresh:** valid successful return + query success → paid; query unknown → stays pending and page is `confirming`; cancelled return → `not_completed` view while pending; anonymous → login with the order `returnUrl`; non-owner → 404; refresh throttled; no confirmation ever from the return alone (query client returning `Unknown`).
- **Checkout:** admin blocked; pending order reused (MoMo and VNPay); rate limit; messages with diacritics; IP parsing.
- **Access:** snapshot end date for a role-Premium subscriber; permanent flag; `ExpireForUserAsync`.
- **Pages (integration):** `/Premium` free, Premium, permanent, no gateway, error; one form with both radio groups and a token; order page in each stage; mock box absent outside Development; history paging, actions, empty; no `bi bi-` class and no "IPN" in learner HTML; Vietnam time formatting; `/Billing/MoMoQr` redirects.
- **Admin:** transactional confirm; state rules; Vietnamese messages.
- **Node:** `billing-core.js` (price per month, saving percent, summary text, polling schedule, countdown formatting, stage handling).
- With D1 = SỬA LUÔN: the three validator tests of 4.6.3.

---

## 7. Local verification without real money
- Start the app for this run with process environment variables only: `MoMo__Enabled=true`, `MoMo__MockModeEnabled=true`, `MoMo__MockSecretKey=<a random value generated for the run, never printed or saved>`, `ASPNETCORE_ENVIRONMENT=Development`. Find how `scripts/start-local.ps1` passes child environment variables; do not edit appsettings or User Secrets.
- VNPay: if the developer's User Secrets already hold sandbox credentials, a sandbox checkout page may be opened to check the redirect and the return handling, **without printing any value**; do not attempt to pay unless the sandbox test card flow is documented in the repo's payment docs. Otherwise VNPay is verified by tests with a fake query client.
- Never use production endpoints. Never create more than the orders the checks need; leave them in the database (orders are history), and list their codes in section 12.

---

## 8. Phases

Run mode: continuous, autonomous, one agent. After each green gate: gate record into section 12 → commit → next phase.

| Phase | Work | Read |
| --- | --- | --- |
| **B0 Baseline** | Clean-tree check (leave `appsettings.Development.json` and `Claude outputs/` unstaged); `git switch master`; verify `2842ca4` or later; create `feature/billing-complete`; build; full + node tests (counts, warnings, known failures); save plan and prompt; read D1–D4; reproduce or disprove F1–F22 (mock flow for F1, F10, F11, F12, F14, F17; code reading and a failing test sketch for the rest); verify the benefit list (4.9); discover `INotificationService`, the pinned-asset process, the business time zone, how env vars reach the app; before-screenshots of `/Premium`, order/QR, result, history at 1440 and 390 into `.tmp/billing/before/` | 0–4, 7 |
| **B1 Core** | Tests first: state machine, confirmation service, unique-conflict fixes; DI lines | 4.1, 4.2 |
| **B2 IPN** | Tests first: IPN through the core, failure-transition rules, MoMo body handling | 4.3 |
| **B3 Queries** | Tests first: VNPay three-way + amount, MoMo query client, throttle | 4.4 |
| **B4 Return, refresh, reconciliation** | Tests first: confirm on return, refresh endpoint, status JSON, reconciliation scope | 4.5 |
| **B5 Access and checkout rules** | Tests first: snapshot, lazy expiry, sign-out mitigation (or D1 fix), checkout rules, messages, IP | 4.6, 4.7, 4.9 |
| **B6 UI foundation** | `billing.css`, kit scope, partials, status mapping, `BillingTime`, `billing-core.js` + node tests, QR script decision | 5 intro, 5.4, 4.9 |
| **B7 Pricing page** | 5.1 | 5.1 |
| **B8 Order page** | 5.2; return handlers redirect to it; `/Billing/MoMoQr` redirect; remove `PaymentResult.cshtml`/`MoMoQr.cshtml` once unused | 5.2 |
| **B9 History** | 5.3 | 5.3 |
| **B10 Admin fixes and header label** | 4.8, 5.5 | 4.8, 5.5 |
| **B11 Docs** | Payment docs, tracker, known-issues (incl. the proposal for Châu), bug-fix-log, project-structure | 0.4 |
| **B12 Full gate** | Whole gate on every billing page and callback; end-to-end runs (8.5); after-screenshots into `.tmp/billing/after/`; final self-review against section 10 | all |

### 8.1 Phase gate
| Gate | Check |
| --- | --- |
| G1 | build; warnings ≤ B0 |
| G2 | area tests: `Billing*`, `MoMo*`, `VnPay*`, `Ipn*`, `Subscription*`, `Premium*`, `Payment*`, `AdminBilling*` |
| G3 | full suite; counts not below B0 plus new tests |
| G4 | node tests; only B0 failures may remain |
| G5 | contracts: the four provider URLs and their responses, signature helpers untouched (`git diff` empty for both helpers), order code format, routes of 2.4 |
| G6 | tokens-only grep on changed CSS; no inline `<style>`/`<script>` in billing views; no `bi bi-` in learner billing files; `git diff --check` |
| G7 | file scope; `Program.cs` diff limited to DI lines (and the D1 block when allowed); `_Layout.cshtml` diff = one string |
| G8 | render at 1440×900, 1366×768, 1024, 768, 390, 320 and 200% zoom; console and network clean |
| G9 | mockup comparison; consistency with `/Sets` and `/Vocabulary` |
| G10 | behaviour checklist (8.4) by mouse and keyboard only |
| G11 | accessibility (5.6) incl. forced colours, reduced motion, no-JS checkout |
| G12 | scripted contrast |
| G13 | regression: Premium gates still pass (`PremiumFeatureGateTests`, speaking/writing premium tests), login and logout, sets image search for a Premium account, admin billing pages open |
| G14 | Clay conformance grep |
| G15 | security: antiforgery tests; owner/404 tests; no secret, key, hash or full signed URL in logs, test output, the diff or section 12 (grep the diff and the run logs for key names and long hex strings); webhooks still anonymous and only those; local `returnUrl` only |
| G16 | money rules: a test proves that no code path marks an order paid without (a) a valid provider callback or (b) a verified provider query with a matching amount or (c) an admin action; grep shows `Status = PaymentOrderStatuses.Paid` only inside `PaymentConfirmationService` |

### 8.2 Failure and stop rules
- A failed item: fix and rerun the whole gate; at most 3 attempts; then "deferred" (phase-caused, reverted) or "blocked" (out of scope, evidence).
- **Stop the run** if green requires: a forbidden file, a migration, a real payment or live credential use, printing a secret, or a broader change to `Program.cs` / account code than allowed. Also stop if B0 shows that a finding of severity A is wrong in a way that changes the design (record why).
- Never skip, weaken or delete a test; never call a skipped check passed.

### 8.3 Gate record (append to section 12 before each commit)
Phase, date, commit subject; G1–G16 each with pass / n-a / deferred / blocked and one line of evidence; decisions; mockup differences; owned tests changed and why; order codes created.

### 8.4 Behaviour checklists
- **B1–B5 (by tests and the mock flow):** every outcome of 4.2; a duplicate callback is acknowledged and changes nothing; a failure callback cannot move `manual_review`/`refunded`/`paid`; a late success goes to manual review for both providers; malformed MoMo body → 204; a successful return with an unknown query stays pending; with a successful query the order is paid exactly once; refresh is throttled; an admin cannot buy; a second click reuses the pending order; the snapshot shows the end date.
- **B7:** plan and method selection update the summary and button; preselection; extension date correct for a Premium buyer; permanent accounts see no pay button; no gateway → notice; error message shown and focused; double click sends one request; works without JavaScript.
- **B8:** MoMo waiting shows QR, countdown and steps; mock box only in Development; mock confirm → page becomes paid (after the login step if the session ends, per D1); expiry while waiting → not-completed with "Thanh toán lại"; confirming polls and "Kiểm tra lại ngay" works and is throttled; paid shows receipt and end date; cancelled/failed/expired/refunded texts; non-owner 404; hidden tab pauses polling.
- **B9:** paging; actions per stage; empty state; times in Vietnam time; long codes do not break the layout at 390.
- **B10:** admin confirm is atomic and sets the activation time; messages in Vietnamese; header label changed.

### 8.5 End-to-end runs (B12)
With synthetic accounts from `.appdata/local/access.json` (never print passwords):
1. Standard learner → `/Premium` → yearly + MoMo (mock) → order page → "Giả lập thanh toán thành công" → (login again if asked) → paid page with the end date → `/Premium` shows "Bạn đang dùng Premium … còn n ngày" → history shows the order and "Xem biên nhận" → a Premium-only feature of another module opens.
2. Same learner buys the monthly plan → end date extends by 30 days from the previous end.
3. A second learner starts MoMo, lets the order expire (shorten by one targeted update of that order's `ExpiresAtUtc`; record the statement) → not-completed page → "Thanh toán lại".
4. Admin opens `/Premium` → no pay button.
5. Another learner requests someone else's order URL → 404.
Record database evidence (status, subscription dates, events) without any secret.

---

## 9. Constraints
- One agent, local only. Granted: local commits on `feature/billing-complete`, one per phase, via `tct-commit-message`. Not granted: push, PR, merge, rebase, reset, deploy.
- Never stage `appsettings*.json`, `Claude outputs/`, `.tmp/`, screenshots.
- Never reset or reseed the database; no paid AI calls; no real payment; never print, log or store credentials, keys, hash secrets or full signed payment URLs.
- Plan and code comments in English; UI copy in Vietnamese exactly as written here.

## 10. Definition of Done
- [ ] Every finding F1–F22 is fixed, mitigated as decided, or disproved with evidence.
- [ ] One confirmation path and one state machine; G16 proves it.
- [ ] A paid order is confirmed without an IPN (verified query) and never from the return alone.
- [ ] Provider URLs, response codes and signatures unchanged.
- [ ] `/Premium`, the order page and the history match the mockups in every state, on laptop and phone, with keyboard, no-JS checkout, forced colours and reduced motion.
- [ ] Learner copy is plain Vietnamese with diacritics; times are Vietnam time; icons render; no false feature claim.
- [ ] Premium users see their end date; buying again extends it; admins cannot buy.
- [ ] Admin confirmation is atomic; admin messages in Vietnamese.
- [ ] The sign-out problem is handled per D1 and the proposal is written.
- [ ] All tests green; other owners' tests unedited; no migration; no secret anywhere.
- [ ] Payment docs, tracker, known-issues and section 12 updated.

## 11. Risks
| Risk | Mitigation |
| --- | --- |
| A refactor of the confirmation path changes money behaviour | Tests first; existing IPN tests stay green; G16 |
| Provider query formats differ from the repo's documents | Three-way result with `Unknown` as the safe default; MoMo query disabled unless configured; recorded as unverified against live sandboxes |
| F1 cannot be fully solved in scope | Mitigation + proposal; D1 lets the owner allow the real fix |
| Polling load | Backoff, hidden-tab pause, 10-minute cap, query throttle |
| `Program.cs` DI edit against the team rule | Limited to registration lines, checked by G7, recorded for the lead |
| Secrets leaking into notes or logs | G15 grep; never echo configuration values |
| Admin and learner status labels drift | One shared mapping |
| Old links to `/Billing/MoMoQr` and old tests | Redirect kept; owned tests updated with reasons |

## 12. Execution notes
(The agent appends B0 discovery notes — including the verdict on each finding and the verified benefit list — and one gate record per phase here.)

### B0 — Baseline, discovery and verdicts (2026-11-17)

**Base.** `master` at `2842ca4` verified; branch `feature/billing-complete` created from it. `appsettings.Development.json` (pre-existing local edit) and `Claude outputs/` stay unstaged.

**Baselines (before any change).**
- Build: 0 errors, 58 warnings (full rebuild of the test project, which builds the app).
- .NET tests: 1867 total = 1831 passed, 1 failed, 35 skipped. The failure is `WritingOptionalEngineContractTests.Evaluate_SharedBudget_StopsSlowCloudBeforeLaterEnginesFinish` (a wall-clock budget test, unrelated to billing; "Optional stages took 6.7 s").
- Node tests (`node --test scripts/tests/*.cjs`): 391 total = 390 passed, 1 failed: `Speaking selection maps original roots and explicit same-level owner replacements; immutable withdrawals survive Git bytes` (a catalog hash, unrelated). These two failures are the "known B0 failures"; every gate may leave only these.
- Running tests regenerates `docs/design-system/email-preview/*`; it is restored with `git checkout -- docs/design-system/email-preview` before each commit.

**Discoveries.**
- `INotificationService` has no per-user create method (only streak/goal/badge generators and an admin broadcast). Decision (4.2 step 7): **no buyer notification is sent**; `PaymentConfirmationService` carries a comment. Not worth widening the notification contract for this task.
- Pinned-asset process: `wwwroot/lib/pinned/provenance.json` + `scripts/tests/test_pinned_browser_assets.py` accept a file whose source matches `https://cdn.jsdelivr.net/npm/<pkg>@x.y.z/…` and whose SHA-256 is recorded; every `~/lib/pinned/<name>` reference in Razor must be manifested. `qrcodejs@1.0.0` qualifies (decision at B6).
- Business time zone: `Services/BusinessDateHelper.cs` (static helper) — `BillingTime` reuses it.
- Environment variables reach the app because `scripts/start-local.ps1` only sets its own variables for the child `dotnet run`; variables set in the calling process (`MoMo__Enabled`, `MoMo__MockModeEnabled`, `MoMo__MockSecretKey`, `ASPNETCORE_ENVIRONMENT`) are inherited. The launcher also forces `Billing__PendingPaymentCleanupWorkerEnabled=false`, `Billing__PremiumExpiryWorkerEnabled=false` and `ReconciliationWorker__Enabled=false` locally, so local runs never expire orders or subscriptions by themselves.
- The local dev DB had **no `PremiumPlans` rows** (`Database__SeedOnStartup=false`). To be able to test checkout, the two plans exactly as `BillingSeedData` defines them were inserted by SQL (idempotent `INSERT … WHERE NOT EXISTS`), and three synthetic learners (`billing-fx1..3@tct.local`, cloned from the synthetic Standard account, ids 6–8) were added so the three synthetic accounts keep their roles. These fixture rows are not production data and are listed in the handoff. The owner's real account (id 4) was never used.
- Provider documents in the repo: `docs/payment-configuration.md` describes the VNPay QueryDR request and its response hash fields; it has no table of `vnp_TransactionStatus` meanings, so section 4.4's fallback applies (only `00` = success, `02` = failed/cancelled, everything else `Unknown`). The MoMo query raw signature (`accessKey&orderId&partnerCode&requestId`) is the public MoMo v2 format, **not verified against a live sandbox**.
- Premium-only features verified against real gates (the list for `PremiumBenefits`, section 4.9):

| Benefit shown to learners | Real gate |
| --- | --- |
| Tạo bài luyện viết bằng AI | `WritingService.Generation.cs:142` (`premium_required` log); private exercises `WritingService.PrivateExercises.cs:49` |
| Thêm video YouTube riêng để luyện nói và shadowing | `SpeakingService.cs:316`, `:440`, `:584`; `SpeakingImportJobService.cs:102` |
| Thêm bài nghe riêng từ YouTube | `ListeningService.cs:625` (create), `:207` (`CanImport`) |
| Dịch lời thoại và tạo quiz bằng AI cho bài nghe | `ListeningService.cs:883` (translate), `:743` (quiz) |
| Đổi tốc độ phát bài nghe (0,5× – 1,5×) | `ListeningService.cs:438` (`CanUseSpeed`); `Views/Study/ListeningPractice.cshtml:172` |
| Điền chi tiết thẻ, gợi ý từ và tìm ảnh minh họa bằng AI | `VocabApiController.cs:32`, `:51`, `:74`; `SetController.cs:41` (`SuggestVocab`) |
| Trợ lý AI không giới hạn lượt mỗi ngày | `AiChatService.cs:792` (Standard limit 15 per day at `:815`) |

  "Không có quảng cáo" is dropped (the app has no ads). The old plan text "toàn bộ tính năng Writing & Speaking" is replaced by the list above.

**Reproduction method.** Local app (Release, port 5100, MoMo mock mode through process variables only) driven by headless Chrome with the fixture learner `billing-fx1`. Screenshots in `.tmp/billing/before/` (ignored). One order was created for that run: `TCT-20261117072308906-BE895D28` (MoMo, yearly, mock-confirmed, paid; subscription for user 6 to 2027-11-17).

**Verdicts (B0).**

| # | Verdict | Evidence |
| --- | --- | --- |
| F1 | **Reproduced** | After "Xác nhận thanh toán giả lập" the next request was redirected to `/Account/Login?ReturnUrl=%2FBilling%2FMoMoQr…`; `/Billing/OrderStatus` answered with a redirect; logging in again showed the order paid. The user is signed out immediately on the next request, not later. Code: `AccountSessionValidator.IsValidAsync` compares the role claim with the database. |
| F2 | Confirmed by code | `GetProviderReturnResultAsync` is display-only; reconciliation only reads `pending`; the cleanup worker expires pending after 15 min. |
| F3 | Confirmed by code | `ReconcileOrderAsync` maps any non-success non-cancel answer to `failed`; `VnPayQueryResult` has no amount. |
| F4 | Confirmed by code | Both IPN handlers only guard `paid`. |
| F5 | Confirmed by code | `ResolveManualReviewConfirmPaid` sets `paid` and saves before activating, no transaction, `ActivatedAtUtc` never set. |
| F6 | Confirmed by code | `IsUniqueConstraintViolation` looks for SQL Server numbers / SQLite text; MariaDB's message is "Duplicate entry … (1062)". Covered by the new tests at B2. |
| F7 | Confirmed by code | `catch (DbUpdateException)` without detaching. |
| F8 | Confirmed by code | The 24 h rule is guarded by `provider == VNPay`. |
| F9 | Confirmed by code | `JsonDocument.ParseAsync` on the raw body, no catch. |
| F10 | **Reproduced** | Premium buyer's `/Premium` shows "Trạng thái tài khoản: Premium — Nâng cấp để truy cập các tính năng AI nâng cao." with no end date (role is Premium so the date is never loaded). |
| F11 | **Reproduced** | `/Premium` has 13 `bi` icons; loaded icon stylesheets: Font Awesome 6.0.0 only; `getComputedStyle(i.bi,'::before').content` is `none`. |
| F12 | Confirmed (page + code) | Two checkout forms and buttons on the page (one per plan for the only ready gateway); `CreateCheckoutAsync` always inserts a new order; `ProviderPaymentUrl` is not saved for VNPay; no admin guard. |
| F13 | Confirmed by code | Anonymous valid return redirects to login with `returnUrl=/Billing/History`. |
| F14 | Confirmed by code | `PaymentResultViewModel.Message` mentions "IPN"; label "Manual review"; no polling on that page. |
| F15 | Confirmed by code | "Không có quảng cáo"; disabled buttons read "chưa khả dụng/chưa cấu hình". |
| F16 | Confirmed by code | Messages without diacritics in `BillingController`, `MoMoGateway`, `BillingManagementController`. |
| F17 | Confirmed by code; **not visible locally** | `ToLocalTime()` uses the server zone; this machine is UTC+7 so History showed the right time (14:23 for 07:23 UTC). A UTC host would be seven hours off. |
| F18 | Confirmed by code | CDN `qrcodejs` without integrity; inline `<style>`/`<script>`. |
| F19 | Confirmed by code | First `X-Forwarded-For` value used unchecked; `::1` is sent as `vnp_IpAddr`. |
| F20 | Confirmed by code | `GetPaymentHistoryAsync` returns every order; the table has no action. |
| F21 | Confirmed by code | `PremiumExpiryWorker` runs hourly (and is disabled locally). |
| F22 | **Reproduced** | Header link text read "Upgrade: Premium". |

No severity-A finding was disproved, so no design change is needed.

**Orders created during checks (cumulative).** `TCT-20261117072308906-BE895D28` (B0).
