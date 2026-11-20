# Billing: full verification + payOS integration — implementation plan

Save as `docs/superpowers/plans/2026-11-20-billing-verification-and-payos.md`.

- **Owner:** Huỳnh Phú Trọng (Billing).
- **Executor:** one Claude Code agent, `IMPLEMENT_LOCAL`, continuous run B0 → B9.
- **Two goals:**
  1. **Prove the current billing logic is correct.** Audit it, add missing tests, then run real end-to-end checks with self-created test accounts (VNPay sandbox, MoMo mock or sandbox), and fix every defect found.
  2. **Add payOS** as a third payment method through the existing `IPaymentGateway` abstraction:
     - checkout link, return/cancel, webhook, status query, reconciliation, health, admin labels, UI choice;
     - fully tested;
     - no real money moved by the agent.

---

## 0. Read first

### 0.1 Rules
1. `AGENTS.md`, `CLAUDE.md`, `DESIGN.md`, `docs/payment-configuration.md`, `docs/payment-go-live-checklist.md`, `docs/payment-operations-runbook.md`, `SECRETS.md`.
2. This plan.
3. The official payOS documentation (`https://payos.vn/docs`, API reference, webhook page). **Where payOS docs and this plan differ, the docs win.** Record the difference in section 12 before coding.

### 0.2 Repo skills
`tct-superpowers`, `tct-engineering`, `tct-api-endpoint`, `tct-security-audit`, `tct-ui-ux-pro-max`, `tct-ui-review`, `tct-commit-message`. Install nothing into the repo.

### 0.3 Safety rules (non-negotiable)
- **No real money.** The agent never completes a real payment and never enters real card or bank data.
  - VNPay: the sandbox only, with the official sandbox test card from VNPay's sandbox documentation.
  - MoMo: mock mode or the official test environment only.
  - payOS: create and cancel links, and **simulated, correctly signed** webhooks. A real transfer is done **only by Trọng himself**, optionally, at the end (B8).
- **Secrets:** payOS `ClientId`, `ApiKey` and `ChecksumKey` only via `dotnet user-secrets` or environment variables on the local machine, as `SECRETS.md` describes.
  - Never in appsettings, Git, logs, screenshots, test fixtures, reports or the chat.
  - Tests use fake keys generated in the test.
- **Data:**
  - local Docker DB only; never reset or reseed;
  - test accounts are created by the agent with test emails `qa.billing.<n>@tct.local` (or the domain the local mail sink accepts);
  - never touch real users' orders or subscriptions;
  - admin actions (refund, manual confirm) only on orders of test accounts.
- **Never** call `confirm-webhook` against payOS with a production URL, and never change payOS dashboard settings. A local tunnel URL is used only if Trọng provides one (B8).

---

## 1. Current system (verified 2026-11-20 on `master` 5e8e92b; reproduce at B0)

| Area | Facts |
| --- | --- |
| Gateways | `IPaymentGateway { ProviderName, IsEnabled, CreateCheckoutAsync(orderCode, amountVnd, description, clientIp), ProcessReturnAsync(query), ProcessIpnAsync(params) }`. Implementations `VnPayGateway` (`Services/Billing/VnPay/`), `MoMoGateway` (`Services/Billing/MoMo/`). Both registered as `IPaymentGateway` in `Program.cs` ≈177–186. |
| Results | `ProviderCheckoutResult` (RedirectUrl, ProviderRequestId, ProviderPaymentUrl, ProviderDeepLink, ProviderQrCodePayload, error), `ProviderCallbackResult`, `ProviderIpnResult`. |
| Order | `PaymentOrder` fields:<br>- Id (long), OrderCode string `TCT-yyyyMMddHHmmssfff-XXXXXXXX`, UserId, PlanId, Provider, AmountVnd, Currency, Status;<br>- RawStatus, Created/Updated/Expires/Paid/Confirmed/Activated timestamps;<br>- ProviderTransactionId, ProviderRequestId, ProviderPaymentUrl, ProviderDeepLink, ProviderQrCodePayload, ProviderResponseCode, ProviderTransactionStatus;<br>- BankCode, BankTransactionNo, CardType, PayType, FailureMessage, ReturnPayloadJson, IpnPayloadJson;<br>- PaymentEvents. |
| States | `PaymentOrderStatuses`: pending, paid, failed, cancelled, expired, refunded, partially_refunded, manual_review. `PaymentOrderStateMachine`. Subscriptions: active, expired, cancelled, revoked. Events idempotency key (Provider, EventType, EventKey). |
| Services | `BillingService`, `IpnService` (611 lines), `PaymentConfirmationService` (single confirmation path), `SubscriptionService`, `PremiumAccessService`, `PaymentReconciliationService` (+ `IVnPayQueryClient`, `MoMoQueryClient`), `PaymentProviderHealthService`, `PaymentAuditService`, `PaymentQueryService`, `PricingPageBuilder`, `OrderPageBuilder`. Worker `Workers/PaymentReconciliationWorker` (`ReconciliationWorker` config). |
| Routes | - `GET /Premium` (Pricing)<br>- `POST /Billing/Checkout`<br>- `GET /Billing/History`<br>- `GET /Billing/Order/{orderCode}`<br>- `GET /Billing/OrderStatus`<br>- `POST /Billing/Order/{orderCode}/Refresh`<br>- `GET /Billing/MoMoQr`<br>- `POST /Billing/MoMoMockConfirm` (mock mode only)<br>- `GET /api/billing/vnpay/ipn`<br>- `POST /api/billing/momo/ipn` (always 204)<br>- `GET /Billing/VnPayReturn`<br>- `GET /Billing/MoMoReturn` |
| Views | `Views/Billing/{Pricing, Order, History, InvalidReturn, _BenefitsTable, _OrderStatusChip, _OrderSummary, _PremiumStatus, _Receipt}`. Admin billing pages under `Areas/Admin` (BillingManagement). |
| Tests | About 45 billing-related test files (`Billing*`, `Ipn*`, `MoMo*`, `VnPay*`, `Payment*`, `Premium*`, `Subscription*`, `AdminBilling*`). |

### 1.1 Unknowns to resolve at B0
1. Exactly how `BillingService.StartCheckout` persists `ProviderRequestId` / `ProviderPaymentUrl` from the gateway result, and the order of "save order" vs "call gateway".
2. How a pending order of the same user/plan is reused or blocked (double checkout).
3. The renewal rule: buying while active (extend from the current end, or from now?), and plan upgrade/downgrade.
4. How the role claim (Premium) is refreshed after activation (the earlier "sign-out after payment" issue; check it is fixed and why).
5. The VNPay sandbox configuration in local user-secrets (present? yes/no only).
6. MoMo mock mode switch name, and whether MoMo test credentials exist (yes/no only).
7. Whether registration requires email verification locally, and how the local mail sink works (so the agent can create test accounts).
8. How `PaymentProviderHealthService` decides that a provider is enabled, and how Pricing shows provider choices.
9. Whether `ProviderRequestId` has an index (EXPLAIN on the local DB is fine; no schema change).
10. The **current payOS docs**: confirm every item of section 4.1 and record the URL and date read.

---

## 2. Decisions (defaults; edit before the run)

| ID | Question | Default |
| --- | --- | --- |
| D1 | payOS order code (payOS needs an integer) | The gateway generates a **numeric code** `yyMMddHHmmss` + 3 random digits (15 digits, < 9 007 199 254 740 991), returns it as `ProviderRequestId`, and it is stored on the order. Webhook and return look up by `Provider == "payos" && ProviderRequestId == code`. **No migration.** If B0 shows no index and the table is large, record it as a known issue (index later, with Châu). |
| D2 | Return / cancel pages | `GET /Billing/PayOSReturn` and `GET /Billing/PayOSCancel`. The query string from payOS is **not trusted**: the status is fetched from the payOS API by the order code before anything is confirmed. |
| D3 | Webhook | `POST /api/billing/payos/webhook` (anonymous). It verifies the HMAC signature over `data`, processes the order through the existing single confirmation path, and **always answers HTTP 200** with a small JSON body once the body was readable (payOS retries otherwise), like the MoMo pattern. |
| D4 | payOS checkout UX | Redirect to the payOS hosted `checkoutUrl` (QR + bank list). The order page keeps `Mở lại trang thanh toán` with the stored URL while the order is pending and not expired. |
| D5 | Mock mode | `PayOS:MockMode=true` (Development only; refused in Production by the options validator). It creates a local fake checkout page + `POST /Billing/PayOSMockConfirm` that sends a **correctly signed** webhook to the app itself, the same as `MoMoMockConfirm`. It lets the whole flow run without real keys. |
| D6 | Provider order on Pricing | `payOS (QR ngân hàng)`, `VNPay`, `MoMo`. Only enabled providers are shown. A disabled provider never appears. |
| D7 | Description text sent to payOS | `TCT` + the numeric code, trimmed to the docs' limit (25 chars per the docs as read at B0; use the shorter limit if the docs say so). |
| D8 | Cancel on our side | When the user cancels or the order expires, call the payOS cancel API once (best effort). A failure is logged and retried by reconciliation. |

---

## 3. Part A — verify the existing billing (B1–B3)

### 3.1 Static audit (B1)
Read every billing service, controller and worker. Write findings `A1…An` into section 12, each with:
- file:line, the risk, a reproduction idea, and a proposed test.

**Mandatory checklist:**
1. **Signature checks:** VNPay `vnp_SecureHash` (sorted, encoded exactly as VNPay requires), MoMo HMAC fields order. Constant-time comparison. Required fields present.
2. **Amount and currency:** compared as an exact integer VND against the order. `decimal` → `long` without rounding errors. Mismatch → manual_review.
3. **Idempotency:**
   - the same IPN twice → one activation;
   - return then IPN, IPN then return → one activation;
   - concurrent IPN + return + reconciliation (row lock or unique event key);
   - no double-extended subscription.
4. **State machine:** every transition allowed or blocked as designed. A terminal state never reopens except paid → refunded. Late IPN after expiry → manual_review (> 24 h) or paid (≤ 24 h? confirm the rule).
5. **Expiry:** pending orders expire at `ExpiresAtUtc`. The worker marks them. The user can start a new checkout.
6. **Activation:**
   - the subscription period (days) is right;
   - stacking on an active plan follows the rule (unknown 3);
   - timezone handling uses UTC everywhere and the display is local;
   - the Premium role takes effect without sign-out (unknown 4).
7. **Ownership:** the order page, status, refresh and history are only for the owner (IDOR); the admin sees all.
8. **Open redirect:** return handlers redirect only to internal order pages.
9. **Input:** `planId` valid and active; provider valid and enabled; one pending order per user/plan/provider (or reuse); antiforgery on POST; rate limit on checkout.
10. **Mock endpoints:** unreachable when mock mode is off and in Production.
11. **Logging and audit:** no secrets or full card data in logs, payload JSON or events; the audit trail for admin actions.
12. **Reconciliation worker:** batch size, back-off, exceptions do not kill the worker, the query clients time out.
13. **Notifications:** an activation notification is sent once.
14. **Admin:** refund and manual confirm only in allowed states, with an audit record; the subscription is revoked on refund.
15. **Health:** a provider with missing config is reported disabled, not crashing the Pricing page.

### 3.2 Automated test matrix (B2) — add what is missing; fix defects with tests first

Use the existing test helpers (WebApplicationFactory, in-memory or MariaDB acceptance as the repo does). Each row becomes at least one test (name it in the record).

| # | Scenario | Providers | Expected |
| --- | --- | --- | --- |
| T1 | Happy path: checkout → IPN paid → order paid → subscription active → Premium features unlocked | VNPay, MoMo, payOS | One activation, correct end date |
| T2 | Return arrives before IPN | all | The return verifies (VNPay hash / MoMo signature / payOS API query); one activation; the later IPN = already confirmed |
| T3 | Duplicate IPN (×3, also concurrent) | all | Exactly one activation, events deduped |
| T4 | Invalid signature | all | No state change, event stored as failed/ignored, correct provider response |
| T5 | Amount mismatch (±1 VND, ×10) | all | manual_review, no activation |
| T6 | Unknown order code | all | Provider response "not found", no exception |
| T7 | Failed or cancelled at the provider | all | failed/cancelled, no activation, the user can retry |
| T8 | Expired then paid IPN within the late window / after it | all | Follows the rule (paid or manual_review) |
| T9 | Reconciliation finds paid / failed / still pending | all | The same outcomes as IPN, idempotent |
| T10 | Buy while Premium active | all | The end date extends per the rule; no gap or overlap bug |
| T11 | Two tabs checkout at the same time | — | One pending order (or reuse), no duplicate charge path |
| T12 | IDOR: user B opens or refreshes A's order | — | 404 |
| T13 | Open redirect attempt in the return params | VNPay, MoMo, payOS | Internal redirect only |
| T14 | Disabled provider chosen by a forged form | — | 400, no order |
| T15 | Mock endpoints with mock off / in Production | MoMo, payOS | 404 |
| T16 | Refund by admin | — | Subscription revoked, Premium removed, audit row |
| T17 | Subscription expiry passes | — | Premium removed, pages show Standard |
| T18 | Role refresh | — | Premium visible on the next request without sign-out |
| T19 | Logs and payload JSON contain no secret or key | all | Grep test over captured logs |
| T20 | Worker resilience (query client throws or times out) | all | Logged, the worker keeps running |

### 3.3 Real end-to-end runs (B3) — the browser through the real UI, with self-created accounts
1. **Create accounts** through `/Account/Register` (unknown 7):
   - `qa.billing.01` (fresh);
   - `qa.billing.02` (will buy twice);
   - `qa.billing.03` (cancels);
   - `qa.billing.04` (IDOR attacker).

   Never reuse real emails. Record the account ids (not the passwords) in the report; store the passwords only in the ignored `.appdata/local/`.
2. **VNPay sandbox** (if keys are configured): buy the cheapest plan with the official sandbox test card. Then check:
   - the return page;
   - the IPN (if the sandbox can reach local; otherwise reconciliation via the query API);
   - the order page, history and receipt;
   - Premium features unlocked (open a Premium-only page);
   - no sign-out happened.
3. **MoMo** (mock mode or the test environment): the same checks.
4. Cancel flow, expiry flow (shorten the expiry only through config in a test environment, never in code), buy while active (`qa.billing.02`), the IDOR attempt (`qa.billing.04`).
5. **Admin:** find the test orders, view the details, refund one test order → Premium is removed.
6. Mobile 390 px pass on Pricing, Order and History.
7. **Cleanup:** leave the orders (audit history must not be deleted); mark the test accounts in the report so the team knows they are test data.

If VNPay or MoMo test credentials are missing, record "not verified live", keep the automated coverage, and continue.

---

## 4. Part B — payOS integration (B4–B7)

### 4.1 payOS facts to confirm at B0 against the official docs (record the source URL)

| Item | Expected (verify) |
| --- | --- |
| Base URL | `https://api-merchant.payos.vn` |
| Auth headers | `x-client-id`, `x-api-key` (+ `Content-Type: application/json`) |
| Create link | `POST /v2/payment-requests` with body `orderCode` (int64), `amount` (int VND), `description` (short, limit per docs), `cancelUrl`, `returnUrl`, optional `expiredAt` (Unix seconds), `items`, `buyerName/Email/Phone`, and `signature` |
| Create signature | HMAC-SHA256 (hex, lowercase) with `ChecksumKey` over `amount={a}&cancelUrl={c}&description={d}&orderCode={o}&returnUrl={r}` (alphabetical keys, raw values) |
| Create response | `{ code:"00", desc, data:{ checkoutUrl, qrCode, paymentLinkId, orderCode, amount, status:"PENDING", … }, signature }` |
| Get status | `GET /v2/payment-requests/{orderCode or paymentLinkId}` → `data.status` among PENDING, PROCESSING, PAID, CANCELLED, EXPIRED (+ underpaid if documented), `amountPaid`, `transactions[]` |
| Cancel | `POST /v2/payment-requests/{id}/cancel` with `{ cancellationReason }` |
| Confirm webhook | `POST /confirm-webhook` `{ webhookUrl }` (payOS then sends a test webhook) |
| Webhook | `POST` JSON `{ code, desc, success, data:{ orderCode, amount, description, accountNumber, reference, transactionDateTime, currency, paymentLinkId, code, desc, counterAccount…, virtualAccount… }, signature }`. The signature = HMAC-SHA256 over `data` sorted by key as `k=v&…` (null/undefined → empty string; nested values as the docs say) |
| Return / cancel query | `code`, `id` (paymentLinkId), `cancel`, `status`, `orderCode` — **unsigned**, so never trust it |
| Webhook success | `data.code == "00"` and `code == "00"` |

**The official SDKs (Node/Python/Java/Go) show the same helpers:**
- `createSignatureOfPaymentRequest` for creating links;
- `createSignatureFromObj` for webhooks.

Use their documented behaviour as the reference for test vectors, and include **at least two known-answer vectors** taken from the docs or SDK tests if published. Otherwise compute them with an independent implementation (Python `hmac`) inside the test.

### 4.2 Code to add (all under `Services/Billing/PayOS/`)

**`PayOSOptions`** (section `PayOS`):
- `Enabled` (false by default), `BaseUrl`, `ClientId`, `ApiKey`, `ChecksumKey`, `ReturnPath` (`/Billing/PayOSReturn`), `CancelPath` (`/Billing/PayOSCancel`), `PublicBaseUrl` (for absolute URLs; falls back to the request host only in Development), `MockMode` (false), `TimeoutSeconds` (15);
- `Normalize()` and `Validate()`, like MoMo/VNPay options.

The provider is enabled only if `Enabled` and all three keys are present, or `MockMode` in Development.

**`PayOSSignature`** (pure, static):
- `ForPaymentRequest(amount, cancelUrl, description, orderCode, returnUrl, key)`;
- `ForData(IReadOnlyDictionary<string, string?> data, key)` (sorted, nulls as empty);
- `Verify(expected, actual)` (constant-time).

**`PayOSClient`** (typed `HttpClient`):
- `CreatePaymentLinkAsync`, `GetPaymentAsync(orderCode)`, `CancelAsync(orderCode, reason)`;
- timeouts; maps `code != "00"` to errors;
- never logs keys or the full body (redacts `accountNumber` and `counterAccountNumber` to the last 4 digits).

**`PayOSGateway : IPaymentGateway`** (`ProviderName = "payos"`):
- `CreateCheckoutAsync`:
  - require a whole-number amount;
  - generate the numeric code (D1);
  - description (D7);
  - absolute return/cancel URLs from `PublicBaseUrl`, carrying **our** `orderCode` as a query value;
  - `expiredAt` = the order expiry;
  - sign and call the client;
  - return `Ok(checkoutUrl, providerRequestId: numericCode, providerPaymentUrl: checkoutUrl, deepLink: null, qrCodePayload: qrCode)`.
- `ProcessReturnAsync`:
  - read the numeric code;
  - **call `GetPaymentAsync`**;
  - map PAID → paid (also check `amountPaid == amount`), CANCELLED/EXPIRED → not paid;
  - the rest → pending;
  - `IsVerified` only if the API answered.
- `ProcessIpnAsync`:
  - the dictionary carries the flattened `data.*` fields + `signature` + top-level `code`/`success`;
  - verify with `ForData`;
  - `IsPaid` = `code=="00"` && `data.code=="00"`;
  - `ProviderTransactionId` = `reference`;
  - `ProviderResponseCode` = `data.code`.
- **The confirm-webhook test call** (an unknown order, e.g. the docs' sample order code): a valid signature but an unknown order → acknowledged, ignored, no exception.

**`PayOSQueryClient`** for reconciliation (or reuse `PayOSClient`). Add payOS to `PaymentReconciliationService.ReconcileOpenOrdersAsync`, with the same outcome mapping as the IPN.

**Health:** add payOS to `PaymentProviderHealthService`.

**Mock (D5):** `PayOSMockGateway` behaviour inside `PayOSGateway` when `MockMode`:
- checkout returns a local URL `/Billing/PayOSMock?orderCode=…` (a simple page in the billing style with `Thanh toán thử` and `Hủy`);
- `POST /Billing/PayOSMockConfirm` builds a webhook body signed with the mock key and posts it through the same `IpnService` path;
- this is never available in Production (options validation + endpoint guard + test).

### 4.3 Controller and service wiring
- `BillingController`:
  - `[AllowAnonymous] POST /api/billing/payos/webhook`: read the raw body (size limit 64 KB); parse JSON safely; flatten; `IpnService.ProcessPayOSWebhookAsync`; return `200 {"success":true}` (also for ignored or unknown cases); never 500 (try/catch with logging);
  - `GET /Billing/PayOSReturn?orderCode=…` and `GET /Billing/PayOSCancel?orderCode=…`: the same pattern as VNPay/MoMo returns, through the `BillingService` return handler, then redirect to the internal order page.
  - Mock routes (D5).
- `IpnService.ProcessPayOSWebhookAsync`:
  - the same structure as MoMo: the event record with the idempotency key `(payos, webhook, reference|orderCode)`;
  - signature → order lookup by `ProviderRequestId` → amount/currency check → state machine → `PaymentConfirmationService` (the single confirmation path).
- `BillingService`: allow provider `payos` in checkout; on cancel/expire call `CancelAsync` (D8).
- `PaymentProviders.PayOS = "payos"`; labels in `PaymentOrderLabels` (`payOS` / `Chuyển khoản QR (payOS)`).
- Admin billing pages: provider filter option and label; the order details show payOS fields (paymentLinkId, reference). Bank account numbers are masked.
- `Program.cs`:
  - `AddOptions<PayOSOptions>().BindConfiguration("PayOS").Validate…`;
  - `AddHttpClient<PayOSClient>()`;
  - register `PayOSGateway` as `IPaymentGateway` (same pattern as MoMo);
  - nothing else.
- `appsettings.json`: add a **non-secret** `PayOS` section only: `Enabled:false`, `BaseUrl`, the paths, `MockMode:false`, `TimeoutSeconds`. `appsettings.Development.json` is **not** committed (it is local); document the user-secrets keys in `SECRETS.md` / `docs/payment-configuration.md`.

### 4.4 UI (match the existing billing design and tokens)
- **Pricing → payment method choice:** three radio cards (D6):
  - `Chuyển khoản QR (payOS)`, subtitle `Quét mã bằng app ngân hàng bất kỳ`, a bank/QR icon;
  - `VNPay`, subtitle `Thẻ ATM, Visa, QR VNPay`;
  - `Ví MoMo`.

  The first enabled one is selected by default. Disabled providers are hidden. Keyboard and screen-reader friendly. The existing "Thanh toán" button text stays.
- **Order page:** for payOS pending orders:
  - the button `Mở lại trang thanh toán`;
  - the hint `Sau khi chuyển khoản, trang sẽ tự cập nhật trong vài giây.`;
  - the existing auto-refresh / `Refresh` keeps working through `GetPaymentAsync`.

  For cancelled orders: `Bạn đã hủy thanh toán. Bạn có thể chọn gói và thanh toán lại.`
- **History and receipt:** the provider label `payOS`.
- **Mock page** (Development only): a clear banner `Chế độ thử – không trừ tiền thật`.

---

## 5. File scope

**May add:**
- `Services/Billing/PayOS/**`;
- tests `TCTEnglish.Tests/PayOS*Tests.cs`, `Billing*E2E*` and the matrix tests;
- `Views/Billing/PayOSMock.cshtml`;
- docs.

**May edit:**
- `Services/Billing/**` (fixes + payOS wiring), `Controllers/BillingController.cs`, `Views/Billing/**`, billing CSS/JS;
- `Areas/Admin/**/BillingManagement*` (labels, filter, details);
- `Workers/PaymentReconciliationWorker.cs` (only if needed);
- `Program.cs` (the billing registration lines only);
- `appsettings.json` (the non-secret `PayOS` section only);
- `SECRETS.md`, `docs/payment-*.md`.

**Forbidden:**
- migrations and `Models/**` (no schema change; stop if one seems required);
- account/auth code (Châu);
- shared layouts and CSS;
- other modules;
- new NuGet packages (use `HttpClient` + `System.Security.Cryptography`; **do not** add a payOS SDK package);
- committing any secret or `appsettings.Development.json`.

---

## 6. Phases

| Phase | Work |
| --- | --- |
| B0 | Pull, branch `feature/billing-verify-payos`. Build; full tests; record the counts. Resolve 1.1 (incl. reading the payOS docs; record the facts). Before screenshots of Pricing, Order, History and Admin billing. |
| B1 | Static audit → findings A1…An in section 12. |
| B2 | Test matrix T1–T20 for VNPay/MoMo (payOS rows wait for B5). Fix every confirmed defect, tests first. |
| B3 | Real end-to-end runs (3.3) with self-created accounts. Fix defects found (with a test each). |
| B4 | payOS core: options, signature (known-answer tests), client (fake handler tests), gateway incl. mock mode (unit tests). |
| B5 | payOS wiring: controller routes, IpnService, reconciliation, health, labels, admin. Run the payOS rows of T1–T20. |
| B6 | payOS UI: provider choice, order page states, history, mock page. Responsive 1440/1366/1024/768/390; accessibility; contrast. |
| B7 | payOS end-to-end in **mock mode** through the real browser with a fresh test account: happy path, cancel, duplicate webhook, bad signature, amount mismatch, expiry + reconciliation, Premium unlocked, refund by admin. |
| B8 | **Optional live payOS check, only if Trọng has put real keys in user-secrets.** Create a link for the cheapest plan, open the payOS checkout page (screenshot without personal data), then cancel it from our side → status CANCELLED via the API. If Trọng provides a tunnel URL, he runs `confirm-webhook` himself, or the agent does it **only if he explicitly says so in the chat**; the test webhook is received and ignored safely. **A real transfer is done by Trọng only**, if he wants. The agent then verifies the activation. Otherwise record "live not verified". |
| B9 | Docs: `payment-configuration.md` (the payOS keys, URLs, mock mode), `payment-go-live-checklist.md` (payOS steps), `payment-operations-runbook.md` (payOS webhook, reconciliation, refunds are done in the bank/payOS, not by API), `SECRETS.md`, tracker, `bug-fix-log.md`. Full gate, self-review, report. |

---

## 7. Gates (after each phase; fix and rerun the whole gate; at most 3 attempts per item)

| Gate | Check |
| --- | --- |
| G1 | `dotnet build`, no new warnings in touched files |
| G2 | Billing + payOS tests |
| G3 | Full suite; counts ≥ B0 |
| G4 | Node tests (if billing JS changed) |
| G5 | `has-pending-model-changes` → none (no migration); route list = old + the payOS routes only |
| G6 | `git diff --check`; encoding guard; tokens grep on CSS; no inline blocks |
| G7 | File scope = section 5; `Program.cs` diff = billing lines; `appsettings.json` diff = the non-secret PayOS section |
| G8 | Render Pricing/Order/History/Admin at 1440×900, 1366×768, 390×844; console and network clean |
| G9 | Behaviour: matrix T1–T20 (each provider where applicable) + E2E lists 3.3 / B7 |
| G10 | Accessibility of the provider choice and order states |
| G11 | Security: signatures, constant-time compare, IDOR, open redirect, mock endpoints closed, rate limit, antiforgery, body size limit |
| G12 | Secrets scan of the diff, logs captured by tests, screenshots: no keys, no full account numbers, no passwords |
| G13 | Regression: Premium-gated features (speed, YouTube import, AI quiz/translate, vocab suggest) work for a paid test account and are locked for Standard |
| G14 | Copy: Vietnamese, consistent with the billing pages; no English UI labels |

Never skip, weaken or delete a test. Never touch a forbidden file. Never call a skipped check passed.

## 8. Stop conditions
1. A schema change or a forbidden file seems necessary.
2. The payOS docs contradict a core assumption (e.g. the signature scheme) and the correct behaviour cannot be confirmed from official sources.
3. Any action would move real money or touch real users' data.
4. A gate item fails 3 times and blocks all later phases.

## 9. Risks

| Risk | Mitigation |
| --- | --- |
| Trusting the return query | Always query the payOS API; webhook signature verification |
| Webhook retries or duplicates | Event idempotency key + the single confirmation path |
| Code collision | 15-digit time+random code, a uniqueness check before saving, retry once |
| Description too long → bank rejects | D7 limit from the docs; a test |
| Missing keys crash Pricing | The health service disables the provider; render tests |
| Mock leaking to production | Options validation + endpoint guard + tests |

## 10. Definition of done
- [ ] Findings A1…An each fixed (with a test), disproved or documented.
- [ ] Matrix T1–T20 green for VNPay, MoMo, payOS (rows that apply).
- [ ] Real E2E runs done for each provider available (or "not verified live" with the reason).
- [ ] payOS works end to end in mock mode; live link creation/cancel verified if keys exist.
- [ ] No migration; no secrets in Git/logs; gates green; docs updated; section 12 complete.

## 11. Commits
Local commits per phase via `tct-commit-message`, e.g.:
- `test(billing): cover the payment test matrix`
- `fix(billing): …`
- `feat(billing): add the payOS gateway`
- `feat(billing): wire the payOS webhook and return`
- `feat(billing): let learners pay by bank QR with payOS`
- `docs(billing): document payOS`

No push, PR or merge.

## 12. Execution record
### 12.1 B0 record (2026-11-20)

**Baseline** (branch `feature/billing-verify-payos` from `master` 46643af, already in sync with `origin/master`; `pull-local.ps1` exit 0, shared data: 456 releases checked, 0 operations).
Tests run in `-c Release` because the owner's Debug server (port 5100) holds the Debug DLLs.

| Item | Value |
| --- | --- |
| Build (Release, tests project) | 0 errors, 19 warnings (all outside billing) |
| Billing-related tests (filter Billing/Ipn/MoMo/VnPay/Payment/Premium/Subscription) | 677 passed, 2 skipped, 0 failed |
| Full suite | recorded in 12.2 when it finishes |

**payOS docs read 2026-11-20** (official, Redocly-rendered pages; they win over plan 4.1):

- `https://payos.vn/docs/api/` (create / get / cancel link, confirm-webhook), `https://payos.vn/docs/du-lieu-tra-ve/webhook/`, `https://payos.vn/docs/du-lieu-tra-ve/return-url/`, `https://payos.vn/docs/tich-hop-webhook/kiem-tra-du-lieu-voi-signature/`.
- Confirmed as in plan 4.1: base URL `https://api-merchant.payos.vn`; headers `x-client-id`, `x-api-key`; `POST /v2/payment-requests` with `orderCode` (integer), `amount` (integer), `description`, `cancelUrl`, `returnUrl`, optional `expiredAt` (Unix timestamp, Int32), `signature`; create signature = HMAC-SHA256 (checksum key) over `amount=..&cancelUrl=..&description=..&orderCode=..&returnUrl=..` (alphabetical, raw values); create response `data.checkoutUrl`, `data.qrCode`, `data.paymentLinkId`, `data.status`; `GET /v2/payment-requests/{orderCode|paymentLinkId}` (response `data.id`, `amountPaid`, `amountRemaining`, `status`, `transactions`, `signature`); `POST /v2/payment-requests/{id}/cancel` with `{cancellationReason}`; `POST /confirm-webhook` `{webhookUrl}`; webhook body `{code, desc, success, data{orderCode, amount, description, accountNumber, reference, transactionDateTime, currency, paymentLinkId, code, desc, counterAccount*, virtualAccount*}, signature}`; webhook signature = HMAC-SHA256 over `data` sorted by key as `key=value&..` (payment-requests: **no** `encodeURI`; the `encodeURI` rule is for payouts only; null/undefined = empty string).
- **Differences from the plan (docs win):**
  1. **`description` limit is 9 characters** for bank accounts that are not linked through payOS (plan D7 said 25). D7 is changed to `TCT` + the last 6 digits of the numeric code = 9 characters. Lookup is by `orderCode`, not by description, so the shorter text costs nothing.
  2. **payOS appends `code`, `id`, `cancel`, `status`, `orderCode` (its own numeric code) to returnUrl/cancelUrl.** Plan 4.2 said to carry **our** `orderCode` as a query value: that name would collide with payOS's numeric `orderCode`. Our value is carried as `ref` instead, and the return handler resolves the order by payOS's numeric `orderCode` (== `ProviderRequestId`) and cross-checks `ref`.
  3. Return/cancel `status` values documented: PAID, PENDING, PROCESSING, CANCELLED. `EXPIRED` and `UNDERPAID` are not on those pages; the client treats an unknown status as "not settled" (Unknown) and `EXPIRED` as not paid.
  4. The documented sample (checksum key `1a54716c…7675`, the webhook body above) has the published signature `8d8640d802576397a1ce45ebda7f835055768ac7ad2e0bfb77f9b8f12cca4c7f`: used as a known-answer test vector.
- The two signature check items of 4.1 (create signature, webhook signature) match the docs. The get-status response is signed too (the client does not depend on it).

**1.1 unknowns resolved**

1. `BillingService.CreateCheckoutAsync` saves the `PaymentOrder` (pending, 15 min expiry) first, writes a `checkout_created` event, then calls the gateway; on success it stores `ProviderRequestId`, `ProviderPaymentUrl` (gateway URL, or the VNPay redirect URL if ≤ 1000 chars), deep link and QR payload. A gateway failure marks the order `failed`.
2. A pending, unexpired order of the same user + plan + provider is **reused** (MoMo: the order page; others: the stored `ProviderPaymentUrl`; an order without a stored URL is not resumed and a new one is created). More than 5 pending orders in 15 min → `RATE_LIMITED`. There is no lock: two simultaneous requests can both pass the check (finding A1).
3. Renewal: a purchase while Premium is active stacks on the current end date (`SubscriptionService.ActivateFromPaidOrderAsync`); admin/permanent accounts are blocked with `ALREADY_PERMANENT`.
4. Role refresh: activation writes `User.Role = Premium`; the known issue `BILLING-ROLE-CHANGE-SIGNS-OUT` (role change signs the buyer out; return handlers send the buyer to login with `returnUrl`) stays as documented in `.ai/context/known-issues.md`; B3 checks the current behaviour in the browser.
5. VNPay sandbox config, MoMo mock switch (`MoMo:Enabled` + `MoMo:MockModeEnabled`, Development only): recorded as yes/no in 12.4.
6. Provider enablement: `PaymentProviderHealthService` builds one health row per provider (`Enabled && Configured`); `Pricing` passes it to `PricingPageBuilder`, which shows a provider only when ready and preselects the first available one.
7. Reconciliation: `PaymentReconciliationService` hard-codes VNPay/MoMo in its query; `PaymentQueryService` hard-codes the two query clients; `PaymentConfirmationService.BuildRawStatus` labels every non-VNPay provider as MoMo; `PaymentOrderPresentation.ReturnReportedSuccess` knows only VNPay/MoMo; `IpnService` has one method per provider. All of these need a payOS branch (B5).
8. `ProviderRequestId` index: not checked yet (12.4).

**Full suite baseline (B0):** 3789 passed, 58 skipped, 0 failed, 3847 total (Release, 5 m 37 s). Gate G3 compares against these counts.

### 12.2 Findings (B1 static audit)

| # | Finding | Evidence | Verdict |
| --- | --- | --- | --- |
| A1 | Two simultaneous checkouts create two pending orders (no lock between "open order?" and "create") | `BillingService.CreateCheckoutAsync`; `BillingConcurrentCheckoutTests` red before, green after | **Fixed** (in-process striped gate; multi-instance stays covered by expiry + rate limit) |
| A2 | VNPay and MoMo signatures compared with `string.Equals` (not constant time) | `VnPaySignatureHelper.Verify`, `VnPayQueryClient`, `MoMoSignatureHelper` | **Fixed** (`BillingSignatureComparer`; `BillingSignatureComparerTests`) |
| A3 | MoMo query response is not signature-checked (matches order id + partner code only) | `MoMoQueryClient.QueryOrderAsync` | **Documented** (known issue BILLING-MOMO-QUERY-UNVERIFIED; the signed field list needs a sandbox answer) |
| A4 | `PaymentConfirmationService.BuildRawStatus`, `ReturnReportedSuccess`, `PaymentQueryService`, `PaymentReconciliationService`, `IpnService` and the provider health all know exactly two providers | code reading | Not a defect today; payOS branch is part of B5 |
| A5 | A forged `provider` in the checkout form gives `PROVIDER_UNSUPPORTED` / `PROVIDER_NOT_CONFIGURED`, answered with a redirect to Pricing plus a message (not an HTTP 400); no order is created | `BillingServiceTests` (`PROVIDER_UNSUPPORTED`, `PROVIDER_NOT_CONFIGURED`) | **Disproved as a defect** (fail-closed; plan T14 expects "400", the actual safe behaviour is 302 + message, recorded) |
| A6 | Late rule: a success within 24 h of expiry is paid, after 24 h goes to manual review; admin may resolve manual review | `PaymentConfirmationService` step 5; `PaymentConfirmationServiceTests` | **Confirmed as designed** |
| A7 | Reconciliation worker is opt-in (`ReconciliationWorker:Enabled=false` by default) | `ReconciliationWorkerOptions` | **Documented** (known issue BILLING-RECONCILIATION-WORKER-OFF) |
| A8 | `PaymentQueryService` allows 12 queries per order, one per 20 s | `PaymentQueryService` constants | **Confirmed**; the payOS client follows the same throttle |

Checklist items covered by existing tests (read, not re-derived): amount check with exact integer VND (T5), manual review on mismatch, idempotency by `(Provider, EventType, EventKey)` + `grant:{OrderCode}` event as the concurrency guard, state machine table, IDOR 404 on order/status/refresh, return handlers redirect to the order page only, antiforgery on every POST, signature fields stripped from stored payloads (`SerializeSafePayload`).

### 12.3 Matrix T1–T20 (scenario × provider → test)

Filled per phase; B2 rows below.

| Row | VNPay / MoMo test (existing unless marked **new**) |
| --- | --- |
| T1 | `PendingVnPayOrder_WithVerifiedSuccess_IsPaidOnce`, `MoMoIpnServiceTests.ProcessMoMoIpnAsync_Success_MarksPaidAndCreatesSubscription` |
| T2 | `BillingReturnConfirmationTests` |
| T3 | `IpnServiceTests` / `IpnTransitionTests` (duplicate IPN); concurrency guard `PaymentConfirmationServiceTests` |
| T4 | `MoMoIpnServiceTests.ProcessMoMoIpnAsync_InvalidSignature_DoesNotMarkPaid`, `VnPayGatewayTests`, `MoMoSignatureHelperTests` |
| T5 | `PaymentReconciliationServiceTests.VerifiedSuccess_WithTheWrongAmount_DoesNotConfirm`, `IpnServiceTests` |
| T6 | `IpnServiceTests` (order not found) |
| T7 | `PaymentReconciliationServiceTests.VnPayFailedAnswer_…`, `MoMoFailedAnswer_MapsToCancelledOrFailed` |
| T8 | `PaymentConfirmationServiceTests` (late rule) |
| T9 | `PaymentReconciliationServiceTests` (all rows) |
| T10 | `SubscriptionServiceTests` (stacking) |
| T11 | **new** `BillingConcurrentCheckoutTests` |
| T12 | `BillingOrderEndpointsTests` (404 for another buyer) |
| T13 | **new** `BillingMatrixGapTests.VnPayReturn_/MoMoReturn_IgnoresAnyRedirectTargetInTheQuery`, `Return_WithAnOrderCodeThatNeedsEscaping_…` |
| T14 | `BillingServiceTests` (`PROVIDER_UNSUPPORTED`, `PROVIDER_NOT_CONFIGURED`) |
| T15 | **new** `BillingMatrixGapTests.MoMoMockConfirm_IsNotFound_WhenMockModeIsNotFullyOn` (Production, Staging, mock flags off), `MoMoMockConfirm_NeverConfirmsAnotherBuyersOrder` |
| T16 | `AdminBillingManagementTests`, `AdminBillingStateRulesTests` |
| T17 | `PremiumStatusAndExpiryTests` |
| T18 | `PremiumAccessServiceTests` (role refresh is the known issue; B3 checks the browser) |
| T19 | **new** `BillingMatrixGapTests.MoMoIpn_StoredPayloads_DoNotContainTheSignature`; payOS keys: B5 |
| T20 | `PaymentReconciliationServiceTests.OneBrokenOrder_DoesNotStopTheRest`, `PaymentQueryServiceTests.AClientThatThrows_IsProviderUnavailable_NotACrash` |

### 12.4 Gate records

_(One entry per phase below.)_
