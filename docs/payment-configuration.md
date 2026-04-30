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
- `MoMo__RedirectUrl`
- `MoMo__IpnUrl`
- `MoMo__RequestType`
- `MoMo__Lang`
- `MoMo__MockModeEnabled`

### Billing Workers

- `Billing__PendingPaymentCleanupWorkerEnabled`
- `Billing__PremiumExpiryWorkerEnabled`

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
