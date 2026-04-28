# SECRETS.md - Huong dan cau hinh bi mat

> **QUAN TRONG:** File `appsettings.json` trong repo nay **khong chua bat ky secret that nao**.
> Moi gia tri nhay cam phai duoc cung cap qua User Secrets (dev) hoac bien moi truong (production).

---

## Development

Dung [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```powershell
# Chuyen vao thu muc project chua csproj
cd TCTEnglish

# Database
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;"

# Google OAuth
dotnet user-secrets set "Authentication:Google:ClientId"     "<your-google-client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<your-google-client-secret>"

# Facebook OAuth
dotnet user-secrets set "Authentication:Facebook:AppId"     "<your-facebook-app-id>"
dotnet user-secrets set "Authentication:Facebook:AppSecret" "<your-facebook-app-secret>"

# SMTP (Gmail App Password)
dotnet user-secrets set "SmtpSettings:SenderEmail" "<your-gmail@gmail.com>"
dotnet user-secrets set "SmtpSettings:Password"    "<your-gmail-app-password>"

# VNPay (Sandbox)
dotnet user-secrets set "VNPay:Enabled"     "true"
dotnet user-secrets set "VNPay:TmnCode"     "<your-vnpay-tmn-code>"
dotnet user-secrets set "VNPay:HashSecret"  "<your-vnpay-hash-secret>"
dotnet user-secrets set "VNPay:ReturnUrl"   "https://localhost:7001/Billing/VnPayReturn"
dotnet user-secrets set "VNPay:IpnUrl"      "https://<your-ngrok-or-tunnel>/api/billing/vnpay/ipn"

# MoMo (Sandbox)
dotnet user-secrets set "MoMo:Enabled"      "true"
dotnet user-secrets set "MoMo:PartnerCode"  "<your-momo-partner-code>"
dotnet user-secrets set "MoMo:AccessKey"    "<your-momo-access-key>"
dotnet user-secrets set "MoMo:SecretKey"    "<your-momo-secret-key>"
dotnet user-secrets set "MoMo:RedirectUrl"  "https://localhost:7001/Billing/MoMoReturn"
dotnet user-secrets set "MoMo:IpnUrl"       "https://<your-ngrok-or-tunnel>/api/billing/momo/ipn"

# AI / Pixabay (tuy chon)
dotnet user-secrets set "AI:ApiKey" "<your-gemini-api-key>"
dotnet user-secrets set "Pixabay:ApiKey" "<your-pixabay-api-key>"
```

---

## Production

Dung environment variables voi double-underscore (`__`) thay cho dau `:`:

| Environment Variable | Mo ta |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Authentication__Google__ClientId` | Google OAuth client ID |
| `Authentication__Google__ClientSecret` | Google OAuth client secret |
| `Authentication__Facebook__AppId` | Facebook app ID |
| `Authentication__Facebook__AppSecret` | Facebook app secret |
| `SmtpSettings__SenderEmail` | Gmail address |
| `SmtpSettings__Password` | Gmail App Password |
| `VNPay__Enabled` | `true` de kich hoat VNPay |
| `VNPay__TmnCode` | Terminal merchant code tu VNPay |
| `VNPay__HashSecret` | HMAC-SHA512 secret key tu VNPay |
| `VNPay__BaseUrl` | `https://pay.vnpay.vn/vpcpay.html` (production) |
| `VNPay__ReturnUrl` | URL tra ve sau thanh toan |
| `VNPay__IpnUrl` | URL server-to-server IPN |
| `MoMo__Enabled` | `true` de kich hoat MoMo |
| `MoMo__PartnerCode` | Partner code tu MoMo |
| `MoMo__AccessKey` | Access key tu MoMo |
| `MoMo__SecretKey` | Secret key tu MoMo |
| `MoMo__BaseUrl` | Base URL gateway MoMo |
| `MoMo__CreatePath` | API path tao giao dich |
| `MoMo__RedirectUrl` | URL redirect sau thanh toan |
| `MoMo__IpnUrl` | URL IPN server-to-server |
| `AI__ApiKey` | Gemini API key (tuy chon) |
| `Pixabay__ApiKey` | Pixabay API key (tuy chon) |
| `Billing__PremiumExpiryWorkerEnabled` | `true` de bat worker het han subscription |
| `Billing__PendingPaymentCleanupWorkerEnabled` | `true` de bat worker don dep pending |

---

## Kiem tra cau hinh

Truy cap `/Admin/BillingManagement/Health` (dang nhap Admin) de xem trang thai cau hinh provider ma **khong lo secret**.

---

## Rotate secret da tung lo

Neu repo da tung chua secret that truoc day, can:

1. Rotate tat ca credential lien quan (DB, OAuth, SMTP, VNPay, MoMo, API keys).
2. Thu hoi/revoke credential cu tren portal nha cung cap.
3. Cap nhat lai bang User Secrets (local) hoac environment variables (production).
4. Kiem tra lai source de dam bao khong con literal secret.
