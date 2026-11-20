# Payment Configuration Guide

This guide defines safe configuration for VNPay and MoMo in sandbox/production.
Do not commit live secrets to source control.

## Configuration Sources

Use this priority order:
1. Environment variables (production).
2. Secret manager (development).
3. `appsettings*.json` placeholders only.

## Required Env Vars

### VNPay

- `VNPay__Enabled`
- `VNPay__TmnCode`
- `VNPay__HashSecret`
- `VNPay__BaseUrl`
- `VNPay__ReturnUrl`
- `VNPay__IpnUrl`
- `VNPay__QueryDrUrl`
- `VNPay__QueryIpAddress`
- `VNPay__QueryDrCommand`

### MoMo

- `MoMo__Enabled`
- `MoMo__PartnerCode`
- `MoMo__AccessKey`
- `MoMo__SecretKey`
- `MoMo__BaseUrl`
- `MoMo__CreatePath`
- `MoMo__QueryPath` (default `/v2/gateway/api/query`; used to confirm an order whose IPN was lost, only when the gateway is fully configured)
- `MoMo__RedirectUrl`
- `MoMo__IpnUrl`
- `MoMo__RequestType`
- `MoMo__Lang`
- `MoMo__MockModeEnabled`

### Billing Workers

- `Billing__PendingPaymentCleanupWorkerEnabled`
- `Billing__PremiumExpiryWorkerEnabled`
- `ReconciliationWorker__Enabled` (default `false`; **recommended `true` for staging and production**), `ReconciliationWorker__IntervalMinutes` (default 5), `ReconciliationWorker__MinPendingMinutes` (default 5)

## How an order gets confirmed

1. **IPN** (signed server-to-server callback): `GET /api/billing/vnpay/ipn`, `POST /api/billing/momo/ipn`.
2. **Verified provider query** with the right amount, when the IPN is lost or unreachable (always the case on `localhost`): VNPay QueryDR (`VNPay__QueryDr*` settings; the response signature and amount are checked) and the MoMo query (`MoMo__QueryPath`). Triggered by the buyer's return with a success code, by "Kiểm tra lại ngay", and by the reconciliation worker. At most one query per order every 20 s and 12 in all; an answer that does not settle the order never changes it.
3. **Admin** action on a `manual_review` order.

The return URL itself never confirms an order. Local development has no public IPN URL, so a local purchase is confirmed by the return query (VNPay sandbox credentials needed) or by the mock mode below.

## Local mock mode (MoMo, development only)

For one local run set process environment variables, never `appsettings` or user secrets: `MoMo__Enabled=true`, `MoMo__MockModeEnabled=true`, `MoMo__MockSecretKey=<random value for that run>` (and `ASPNETCORE_ENVIRONMENT=Development`, which `scripts/start-local.ps1` sets). The order page then shows a dashed "Chế độ thử nghiệm" box with "Giả lập thanh toán thành công". Both the box and `POST /Billing/MoMoMockConfirm` are refused (404) in any other environment, and no provider is contacted.

## VNPay

### Sandbox

- `VNPay__BaseUrl=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html`
- Use sandbox `TmnCode` + matching sandbox `HashSecret`.
- `ReturnUrl` and `IpnUrl` must be public HTTPS URLs.
- VNPay cannot call local `localhost` IPN endpoint.

### Production

- `VNPay__BaseUrl=https://pay.vnpayment.vn/vpcpay.html`
- Use production credentials only.
- Keep `ReturnUrl` and `IpnUrl` on same trusted domain.

### Development Example (User Secrets)

```bash
dotnet user-secrets init
dotnet user-secrets set "VNPay:Enabled" "true"
dotnet user-secrets set "VNPay:TmnCode" "YOUR_SANDBOX_TMN_CODE"
dotnet user-secrets set "VNPay:HashSecret" "YOUR_SANDBOX_HASH_SECRET"
dotnet user-secrets set "VNPay:BaseUrl" "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"
dotnet user-secrets set "VNPay:ReturnUrl" "https://YOUR_PUBLIC_DOMAIN/Billing/VnPayReturn"
dotnet user-secrets set "VNPay:IpnUrl" "https://YOUR_PUBLIC_DOMAIN/api/billing/vnpay/ipn"
```

## MoMo

### Sandbox

- `MoMo__BaseUrl=https://test-payment.momo.vn`
- `MoMo__CreatePath=/v2/gateway/api/create`
- Use sandbox `PartnerCode`, `AccessKey`, `SecretKey`.
- `RedirectUrl` and `IpnUrl` must be public HTTPS URLs.

### Production

- Use production `BaseUrl` from MoMo merchant onboarding.
- Disable `MockModeEnabled` outside development.
- Keep separate credentials per environment.

### Development Example (User Secrets)

```bash
dotnet user-secrets set "MoMo:Enabled" "true"
dotnet user-secrets set "MoMo:PartnerCode" "YOUR_SANDBOX_PARTNER_CODE"
dotnet user-secrets set "MoMo:AccessKey" "YOUR_SANDBOX_ACCESS_KEY"
dotnet user-secrets set "MoMo:SecretKey" "YOUR_SANDBOX_SECRET_KEY"
dotnet user-secrets set "MoMo:BaseUrl" "https://test-payment.momo.vn"
dotnet user-secrets set "MoMo:CreatePath" "/v2/gateway/api/create"
dotnet user-secrets set "MoMo:RedirectUrl" "https://YOUR_PUBLIC_DOMAIN/Billing/MoMoReturn"
dotnet user-secrets set "MoMo:IpnUrl" "https://YOUR_PUBLIC_DOMAIN/api/billing/momo/ipn"
dotnet user-secrets set "MoMo:RequestType" "captureWallet"
dotnet user-secrets set "MoMo:Lang" "vi"
dotnet user-secrets set "MoMo:MockModeEnabled" "false"
```

## Signature Validation Test

Run targeted tests:

```bash
dotnet test -c Release --filter "MoMoSignatureHelper|MoMoIpn|VnPaySignatureHelper|VnPayGateway|Ipn"
```

If signature tests fail, do not enable provider in production.

## Provider Health Validation

1. Open `/Admin/BillingManagement/Health`.
2. Confirm provider shows enabled + configured + ready.
3. Any missing field must be fixed before checkout is allowed.

## Emergency Disable Procedure

Disable one provider immediately via env vars:

- `VNPay__Enabled=false` or `MoMo__Enabled=false`

Then restart app and verify health page shows disabled.

## Refund Support Status

- Automatic refund API flow is not implemented in admin.
- Refund must be executed directly in VNPay/MoMo merchant portal.
- Keep internal audit notes and incident reference in admin workflow.

## payOS (bank transfer by QR)

payOS is the third provider (`payos`). The buyer is sent to payOS's hosted page and pays by scanning a QR code with any bank app. Documentation read 2026-11-20: `https://payos.vn/docs/api/`, `/docs/du-lieu-tra-ve/webhook/`, `/docs/du-lieu-tra-ve/return-url/`, `/docs/tich-hop-webhook/kiem-tra-du-lieu-voi-signature/`.

### Settings

| Key | Where | Meaning |
| --- | --- | --- |
| `PayOS:Enabled` | appsettings (non-secret, default `false`) | switches the provider on |
| `PayOS:ClientId`, `PayOS:ApiKey`, `PayOS:ChecksumKey` | **user-secrets / environment only** (`PayOS__ClientId`, ...) | the three keys of the payOS payment channel |
| `PayOS:PublicBaseUrl` | user-secrets / environment | public address of this site (`https://...`, no path); payOS sends the buyer back to `{PublicBaseUrl}/Billing/PayOSReturn` and `/Billing/PayOSCancel` |
| `PayOS:BaseUrl`, `ReturnPath`, `CancelPath`, `TimeoutSeconds` | appsettings | defaults are right for payOS |
| `PayOS:MockMode` | appsettings / environment | **Development only**: a local fake payOS page. The app refuses to start with `MockMode=true` outside Development |

The provider is offered on /Premium only when `Enabled` is true, the three keys are set and `PublicBaseUrl` is a valid HTTPS address (HTTP is accepted only in Development). A provider that cannot be used is not shown at all.

```powershell
cd TCTEnglish
dotnet user-secrets set "PayOS:Enabled" "true"
dotnet user-secrets set "PayOS:ClientId" "<client id>"
dotnet user-secrets set "PayOS:ApiKey" "<api key>"
dotnet user-secrets set "PayOS:ChecksumKey" "<checksum key>"
dotnet user-secrets set "PayOS:PublicBaseUrl" "https://<public address of the site>"
```

### Addresses

| Purpose | Address | Notes |
| --- | --- | --- |
| Return (paid) | `{PublicBaseUrl}/Billing/PayOSReturn?ref={our order code}` | payOS appends `code`, `id`, `cancel`, `status`, `orderCode` (its own number). The query is **not signed**: the app asks the payOS API before it shows or changes anything |
| Cancel | `{PublicBaseUrl}/Billing/PayOSCancel?ref=...` | same handling; the order becomes `cancelled` only when the API says CANCELLED |
| Webhook | `POST {PublicBaseUrl}/api/billing/payos/webhook` | register it once in payOS (`confirm-webhook` or the dashboard). Anonymous; verified by the HMAC-SHA256 signature over `data`; always answers HTTP 200 once the body was readable |

### How an order works

- The order keeps its own code (`TCT-...`). payOS needs an integer: a 15-digit code (`yyMMddHHmmss` + 3 random digits) is generated and stored in `PaymentOrder.ProviderRequestId` (no schema change). Webhook and return find the order by `Provider = payos` and that number.
- The payment description sent to payOS is `TCT` + the last 6 digits of the number: **9 characters, the limit for bank accounts not linked through payOS**.
- The link expires with the order (15 minutes). When the cleanup worker expires an order, it asks payOS to cancel the link (best effort).
- Confirmation goes only through `PaymentConfirmationService` (webhook, return query, refresh and reconciliation all end there); the amount actually paid must equal the order amount.
- Stored webhook payloads contain no signature and keep bank account numbers to their last four digits.

### Local mock mode

`PayOS:Enabled=true` and `PayOS:MockMode=true` in the Development environment (no key needed): checkout goes to `/Billing/PayOSMock`, "Thanh toán thử" sends this app a correctly signed webhook for the buyer's own order (signed with the non-secret `PayOS:MockChecksumKey`), "Hủy thanh toán" follows the cancel address. No network call to payOS is made without keys.

Example (calling shell, then `scripts/start-local.ps1 -NoBrowser`): `$env:PayOS__Enabled='true'; $env:PayOS__MockMode='true'`.

### Refunds

payOS has no refund call used by this app: a refund is made at the bank / in the payOS dashboard, then Premium is revoked in Admin (same as VNPay and MoMo).
