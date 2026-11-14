using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.IO;
using TCTEnglish.Models;
using TCTEnglish.Services.Accounts;
using TCTEnglish.Services.Learning;
using TCTEnglish.Services.Notifications;
using TCTEnglish.ViewModels.Account;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    public class AccountController : BaseController
    {
        private readonly DbflashcardContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IAppEmailSender _emailSender;
        private readonly IAvatarUploadService _avatarUploadService;
        private readonly ILogger<AccountController> _logger;
        private readonly IGoalsService _goalsService;
        private readonly IAccountProfileService _profiles;
        private readonly IAccountSecurityService _security;
        private readonly IAccountDeletionService _deletion;
        private readonly INotificationPreferenceService _preferences;
        private readonly IWebPushSubscriptionService _pushSubscriptions;
        private readonly IOptions<OutboundDeliveryOptions> _outboundOptions;

        public AccountController(
            DbflashcardContext context,
            IWebHostEnvironment webHostEnvironment,
            IAppEmailSender emailSender,
            IAvatarUploadService avatarUploadService,
            ILogger<AccountController> logger,
            IGoalsService goalsService,
            IAccountProfileService profiles,
            IAccountSecurityService security,
            IAccountDeletionService deletion,
            INotificationPreferenceService preferences,
            IWebPushSubscriptionService pushSubscriptions,
            IOptions<OutboundDeliveryOptions> outboundOptions)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _emailSender = emailSender;
            _avatarUploadService = avatarUploadService;
            _logger = logger;
            _goalsService = goalsService;
            _profiles = profiles;
            _security = security;
            _deletion = deletion;
            _preferences = preferences;
            _pushSubscriptions = pushSubscriptions;
            _outboundOptions = outboundOptions;
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("~/Views/Account/AccessDenied.cshtml");
        }

        // GET: /Account/Login
        public IActionResult Login()
        {
            if (User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            ViewData["ActiveTab"] = "login";
            return View("Auth");
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            if (User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            ViewData["ActiveTab"] = "register";
            return View("Auth");
        }

        [HttpGet]
        public IActionResult Premium()
        {
            return Redirect("/Home/Landing#pricing");
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string email, string password, string fullName) // Mapping Username input to FullName
        {
            ViewData["ActiveTab"] = "register"; // Keep user on Register tab if error

            // 1. Validate inputs before hashing / persistence
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
            {
                ViewBag.ErrorMessage = "Vui lòng nhập đầy đủ thông tin.";
                return View("Auth");
            }

            if (!IsValidEmailAddress(email))
            {
                ViewBag.ErrorMessage = "Email không hợp lệ.";
                return View("Auth");
            }

            if (password.Length < 6)
            {
                ViewBag.ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.";
                return View("Auth");
            }

            // 2. Check if email exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
            {
                ViewBag.ErrorMessage = "Email này đã được sử dụng.";
                return View("Auth");
            }

            // 3. Hash password
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            // 4. Create User
            var newUser = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                FullName = fullName,
                CreatedAt = DateTime.UtcNow,
                Role = Roles.Standard
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 5. Redirect to Login tab with success message
            TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập bằng tài khoản vừa tạo.";
            return RedirectToAction("Login");
        }

        // POST: /Account/Login
        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            // Removed ViewData["ActiveTab"] as we are using AJAX now

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || user.AnonymizedAt != null)
            {
                // Return JSON error
                return Json(new { success = false, message = "Email hoặc mật khẩu không đúng." });
            }

            // Social/legacy/malformed hashes must fail closed without SaltParseException → 500.
            if (!TryVerifyLocalPassword(password, user.PasswordHash, out var isPasswordValid) || !isPasswordValid)
            {
                return Json(new { success = false, message = "Email hoặc mật khẩu không đúng." });
            }

            // --- Block / Auto-Unlock check ---
            if (user.Status == UserStatus.Blocked)
            {
                // Lazy Auto-Unlock: if lock has expired, reset and allow login
                if (user.LockExpiry.HasValue && DateTime.UtcNow >= user.LockExpiry.Value)
                {
                    var expiry = user.LockExpiry;
                    var affected = await _context.Users.Where(u => u.UserId == user.UserId && u.Status == UserStatus.Blocked && u.LockExpiry == expiry)
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Offline)
                            .SetProperty(u => u.LockReason, (string?)null).SetProperty(u => u.LockExpiry, (DateTime?)null));
                    await _context.Entry(user).ReloadAsync();
                    if (affected > 0) await _emailSender.TrySendUnlockedNotificationAsync(user.Email, true, HttpContext.RequestAborted);
                    // Fall through to sign-in below
                }
                if (user.Status == UserStatus.Blocked)
                {
                    // Active Block: populate TempData for the auto-popup modal
                    TempData["IsBlocked"] = true;
                    TempData["LockReason"] = user.LockReason ?? "Không rõ lý do.";
                    TempData["LockExpiry"] = user.LockExpiry.HasValue && user.LockExpiry.Value < DateTime.MaxValue
                        ? user.LockExpiry.Value.ToString("dd/MM/yyyy HH:mm:ss (UTC)")
                        : "Vĩnh viễn";

                    return Json(new
                    {
                        success = false,
                        blocked = true
                    });
                }
            }

            await SignInUserAsync(user);

            // Return JSON success
            return Json(new { success = true });
        }

        // GET: /Account/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Xóa external cookie scheme để tránh correlation lỗi khi đăng nhập lại
            try { await HttpContext.SignOutAsync("ExternalCookie"); } catch { }

            // Clear any external authentication cookies to prevent auto-reuse
            foreach (var cookie in HttpContext.Request.Cookies.Keys)
            {
                if (cookie.Contains("External") || cookie.Contains("Correlation") || cookie.Contains(".Google") || cookie.Contains(".Facebook"))
                {
                    Response.Cookies.Delete(cookie);
                }
            }

            return RedirectToAction("Login");
        }

        // --- SOCIAL LOGIN ---

        // GET: /Account/ExternalLogin
        public IActionResult ExternalLogin(string provider)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };

            // Force Google/Facebook to always show the account picker screen
            properties.Items["prompt"] = "select_account";
            properties.Items["login_hint"] = "";

            return Challenge(properties, provider);
        }

        // GET: /Account/ExternalLoginCallback
        public async Task<IActionResult> ExternalLoginCallback()
        {
            // 1. Authenticate and retrieve the external login result
            var result = await HttpContext.AuthenticateAsync("ExternalCookie");

            // If authentication failed or no principal, redirect back to login
            if (result?.Succeeded != true || result.Principal == null)
            {
                return RedirectToAction("Login");
            }

            // 2. Extract claims from the authenticated principal
            var principal = result.Principal;

            var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value
                             ?? principal.FindFirst("email")?.Value;

            var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value
                            ?? principal.FindFirst("name")?.Value;

            // 3. Extract Google profile picture URL (handles multiple claim types gracefully)
            var pictureClaim = principal.FindFirst("picture")?.Value
                               ?? principal.FindFirst("urn:google:picture")?.Value
                               ?? principal.FindFirst("image")?.Value
                               ?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/picture")?.Value;

            if (string.IsNullOrEmpty(emailClaim))
            {
                ViewBag.ErrorMessage = "Không thể lấy thông tin Email từ tài khoản xã hội.";
                return View("Auth");
            }

            // 4. Check if user already exists in database
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == emailClaim);

            if (user == null)
            {
                // New user → auto-register with Google avatar as default
                user = new User
                {
                    Email = emailClaim,
                    FullName = nameClaim ?? "Social User",
                    PasswordHash = "SOCIAL_LOGIN_" + Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Role = Roles.Standard,
                    AvatarUrl = pictureClaim // Save Google profile picture as default avatar
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Existing user → update AvatarUrl if Google picture has changed
                if (!string.IsNullOrEmpty(pictureClaim) && user.AvatarUrl != pictureClaim)
                {
                    user.AvatarUrl = pictureClaim;
                    await _context.SaveChangesAsync();
                }

                // --- Block / Auto-Unlock check for social login ---
                if (user.Status == UserStatus.Blocked)
                {
                    if (user.LockExpiry.HasValue && user.LockExpiry.Value <= DateTime.UtcNow)
                    {
                        user.Status = UserStatus.Offline;
                        user.LockReason = null;
                        user.LockExpiry = null;
                        await _context.SaveChangesAsync();
                        await _emailSender.SendUnlockedNotificationAsync(user.Email, isAutoUnlock: true);
                    }
                    else
                    {
                        var lockExpiryStr = user.LockExpiry.HasValue && user.LockExpiry.Value < DateTime.MaxValue
                            ? user.LockExpiry.Value.ToString("dd/MM/yyyy HH:mm:ss (UTC)")
                            : "Vĩnh viễn";
                        TempData["BlockedReason"] = user.LockReason ?? "Không rõ lý do.";
                        TempData["BlockedExpiry"] = lockExpiryStr;
                        await HttpContext.SignOutAsync("ExternalCookie");
                        return RedirectToAction("Login");
                    }
                }
            }

            // 5. Sign in with our app's cookie claims
            await SignInUserAsync(user);

            // 6. Clean up the temporary external cookie
            await HttpContext.SignOutAsync("ExternalCookie");

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/LinkFacebook
        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpGet]
        public IActionResult LinkFacebook()
        {
            var redirectUrl = Url.Action("LinkExternalCallback", "Account", null, Request.Scheme);
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            // Ensure select_account to allow user to pick the correct account
            properties.Items["prompt"] = "select_account";
            return Challenge(properties, "Facebook");
        }

        // GET: /Account/LinkGoogle
        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpGet]
        public IActionResult LinkGoogle()
        {
            var redirectUrl = Url.Action("LinkExternalCallback", "Account", null, Request.Scheme);
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            properties.Items["prompt"] = "select_account";
            return Challenge(properties, "Google");
        }

        // GET: /Account/LinkExternalCallback
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> LinkExternalCallback()
        {
            var result = await HttpContext.AuthenticateAsync("ExternalCookie");
            if (result?.Succeeded != true || result.Principal == null)
            {
                TempData["ErrorMessage"] = "Không thể kết nối với tài khoản xã hội.";
                return RedirectToAction("Settings");
            }

            var emailClaim = result.Principal.FindFirst(ClaimTypes.Email)?.Value
                             ?? result.Principal.FindFirst("email")?.Value;

            // Xóa external cookie tạm
            await HttpContext.SignOutAsync("ExternalCookie");

            if (string.IsNullOrEmpty(emailClaim))
            {
                TempData["ErrorMessage"] = "Không thể lấy Email từ tài khoản xã hội để liên kết.";
                return RedirectToAction("Settings");
            }

            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            
            if (user == null) return RedirectToAction("Login");

            if (!string.Equals(user.Email, emailClaim, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = $"Email tài khoản xã hội ({emailClaim}) không khớp với email hiện tại của bạn. Hãy đảm bảo dùng chung email.";
                return RedirectToAction("Settings");
            }

            // Thành công, email khớp. Vì DB không lưu GoogleId riêng lẻ mà match qua email,
            // chỉ cần thông báo cho user biết là account đã liên kết thành công.
            TempData["SuccessMessage"] = "Xác nhận liên kết thành công! Bạn có thể dùng tài khoản xã hội này để đăng nhập.";
            return RedirectToAction("Settings");
        }

        private async Task SignInUserAsync(User user)
        {
            var normalizedRole = Roles.Normalize(user.Role);
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                new Claim(ClaimTypes.Role, normalizedRole)
            };

            // Add AvatarUrl Claim if exists
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                claims.Add(new Claim("AvatarUrl", user.AvatarUrl));
            }

            claims.Add(new Claim(AccountSecurityClaims.Version, AccountSecurityClaims.FormatVersion(user.SecurityVersion)));

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
        }

        // GET: /Account/ForgotPassword
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // SECURE: Dùng thông báo chung để tránh email enumeration attack
            const string genericMessage = "Nếu email này tồn tại trong hệ thống, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu.";

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                // SECURE: Không tiết lộ email có tồn tại hay không
                ViewBag.Message = genericMessage;
                return View("ForgotPasswordConfirmation");
            }

            // Generate secure token and set expiry (15 minutes)
            var token = Guid.NewGuid().ToString();
            user.ResetPasswordToken = token;
            user.ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            // Build the reset link
            var resetLink = Url.Action("ResetPassword", "Account",
                new { token = token, email = user.Email }, Request.Scheme);

            // FIX: Delegate to injected IAppEmailSender instead of duplicating SMTP logic in controller
            await _emailSender.SendPasswordResetAsync(user.Email, resetLink!);

            ViewBag.Message = genericMessage;
            return View("ForgotPasswordConfirmation");
        }

        // GET: /Account/Profile
        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile(int habitDays = 7)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return RedirectToAction("Login");

            var model = await _profiles.BuildAsync(userId, Request.IsHttps, Request.Query["tab"], null, windowDays: habitDays);
            if (model == null)
            {
                return RedirectToAction("Login");
            }

            return View("~/Views/Account/Profile.cshtml", model);
        }

        // POST: /Account/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UpdateProfile(UpdateProfileInput model)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");
            if (string.IsNullOrWhiteSpace(model.FullName) || model.FullName.Trim().Length > 100)
            {
                ModelState.AddModelError(nameof(model.FullName), "Họ tên cần từ 1 đến 100 ký tự.");
            }

            if (!ModelState.IsValid)
            {
                var invalid = await _profiles.BuildAsync(userId, Request.IsHttps, "profile", model.FullName);
                return View("~/Views/Account/Profile.cshtml", invalid);
            }

            var result = await _profiles.UpdateAsync(userId, model.FullName, model.Avatar);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Không lưu được hồ sơ.");
                var failed = await _profiles.BuildAsync(userId, Request.IsHttps, "profile", model.FullName);
                return View("~/Views/Account/Profile.cshtml", failed);
            }

            if (result.ReissueSession)
            {
                var user = await _context.Users.AsNoTracking().FirstAsync(row => row.UserId == userId);
                await SignInUserAsync(user);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Profile");
        }

        // GET: /Account/Settings
        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpGet]
        public IActionResult Settings()
        {
            // Keep bookmarked and emailed settings URLs on the same surface as the sidebar.
            return RedirectToAction("Profile", new { tab = "security" });
        }

        // POST: /Account/UpdateEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account-security")]
        public async Task<IActionResult> UpdateEmail(SecuritySettingsViewModel model)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return RedirectToAction("Login");

            var confirmUrl = Url.Action("ConfirmEmailChange", "Account", new { token = "TOKEN" }, Request.Scheme)
                ?.Replace("TOKEN", "{0}", StringComparison.Ordinal)
                ?? "/Account/ConfirmEmailChange?token={0}";
            var result = await _security.RequestEmailChangeAsync(
                user,
                model.Email,
                model.CurrentPassword,
                model.ProofCode,
                confirmUrl,
                HttpContext.RequestAborted);
            return await SecurityResultAsync(user, model.ReturnSurface, result, model.Email);
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account-security")]
        public async Task<IActionResult> ChangePassword(SecuritySettingsViewModel model)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return RedirectToAction("Login");

            var result = await _security.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.ProofCode,
                model.NewPassword,
                model.ConfirmPassword);
            return await SecurityResultAsync(user, model.ReturnSurface, result);
        }

        // POST: /Account/DeleteAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account-security")]
        public async Task<IActionResult> DeleteAccount(string? confirmText, string? currentPassword, string? proofCode, string? returnSurface)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");

            var result = await _deletion.DeleteAsync(userId, confirmText, currentPassword, proofCode);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Error;
                return returnSurface == "profile"
                    ? RedirectToAction("Profile", new { tab = "security" })
                    : RedirectToAction("Settings");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            try { await HttpContext.SignOutAsync("ExternalCookie"); } catch { }

            TempData["SuccessMessage"] = "Tài khoản của bạn đã được xóa. Dữ liệu cá nhân đã gỡ khỏi tài khoản này; bản sao lưu máy chủ không bị xóa bởi thao tác này.";
            return RedirectToAction("Login");
        }
        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            var model = new ResetPasswordViewModel
            {
                Token = token,
                Email = email
            };
            return View(model);
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Reject missing/expired before hashing. Concurrent POSTs: only one ExecuteUpdate wins.
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(
                u => u.Email == model.Email && u.ResetPasswordToken == model.Token);

            if (user == null)
            {
                ModelState.AddModelError(nameof(model.Token), "Liên kết đặt lại mật khẩu không hợp lệ.");
                return View(model);
            }

            if (!user.ResetPasswordTokenExpiry.HasValue || user.ResetPasswordTokenExpiry.Value < DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(model.Token), "Liên kết đặt lại mật khẩu đã hết hạn hoặc không hợp lệ.");
                return View(model);
            }

            var newHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            var affected = await _context.Users
                .Where(u => u.UserId == user.UserId
                    && u.ResetPasswordToken == model.Token
                    && u.ResetPasswordTokenExpiry != null
                    && u.ResetPasswordTokenExpiry > DateTime.UtcNow)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.PasswordHash, newHash)
                    .SetProperty(u => u.ResetPasswordToken, (string?)null)
                    .SetProperty(u => u.ResetPasswordTokenExpiry, (DateTime?)null)
                    .SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1));

            if (affected == 0)
            {
                ModelState.AddModelError(nameof(model.Token), "Liên kết đặt lại mật khẩu không hợp lệ.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpGet]
        public IActionResult ConfirmEmailChange(string? token)
        {
            ViewData["Title"] = "Xác nhận email";
            ViewBag.Token = token ?? string.Empty;
            return View("~/Views/Account/ConfirmEmailChange.cshtml");
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account-security")]
        public async Task<IActionResult> ConfirmEmailChange(string? token, string? unused)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");
            var user = await _context.Users.FirstOrDefaultAsync(row => row.UserId == userId);
            if (user == null) return RedirectToAction("Login");
            _ = unused;
            var result = await _security.ConfirmEmailChangeAsync(userId, token);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Error;
                return RedirectToAction("Settings");
            }

            var refreshed = await _context.Users.AsNoTracking().FirstAsync(row => row.UserId == userId);
            await SignInUserAsync(refreshed);
            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Settings");
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account-security")]
        public async Task<IActionResult> RequestAccountProof(string purpose, string? returnSurface)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");
            var user = await _context.Users.FirstOrDefaultAsync(row => row.UserId == userId);
            if (user == null) return RedirectToAction("Login");
            var result = await _security.SendProofAsync(user, purpose, null);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded ? result.Message : result.Error;
            return returnSurface == "profile"
                ? RedirectToAction("Profile", new { tab = "security" })
                : RedirectToAction("Settings");
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("account-security")]
        public async Task<IActionResult> VerifyCurrentEmail(string? proofCode, string? returnSurface)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");
            var result = await _security.VerifyCurrentEmailAsync(userId, proofCode, HttpContext.RequestAborted);
            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded ? result.Message : result.Error;
            return returnSurface == "profile"
                ? RedirectToAction("Profile", new { tab = "security" }) : RedirectToAction("Settings");
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNotificationPreferences(NotificationPreferenceForm model)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login");
            var saved = await _preferences.SaveAsync(userId, new NotificationPreferenceForm
            {
                DailyReminderEnabled = model.DailyReminderEnabled,
                ReminderHour = model.ReminderHour,
                ReminderMinute = model.ReminderMinute,
                TimeZoneId = model.TimeZoneId,
                PushOptIn = model.PushOptIn,
                WeeklyEmailOptIn = model.WeeklyEmailOptIn
            });
            TempData[saved.Succeeded ? "SuccessMessage" : "ErrorMessage"] = saved.Succeeded
                ? "Đã lưu tùy chọn thông báo."
                : saved.Error;
            return model.ReturnSurface == "profile"
                ? RedirectToAction("Profile", new { tab = "security" })
                : RedirectToAction("Settings");
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpGet]
        public IActionResult PushKey()
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            return Json(new
            {
                accountCode = AccountDisplayCode.Format(userId),
                configured = _outboundOptions.Value.PushConfigured,
                publicKey = _outboundOptions.Value.PushConfigured ? _outboundOptions.Value.VapidPublicKey : null
            });
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PushSubscribe([FromBody] PushSubscriptionInput input)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            var result = await _pushSubscriptions.SubscribeAsync(userId, input);
            return result.Succeeded ? Ok(new { success = true })
                : result.FreshSubscriptionRequired
                    ? Conflict(new { error = result.Error, freshSubscriptionRequired = true })
                    : BadRequest(new { error = result.Error });
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PushUnsubscribe([FromBody] PushSubscriptionInput input)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            var removed = await _pushSubscriptions.UnsubscribeAsync(userId, input.Endpoint);
            return removed ? Ok(new { success = true }) : NotFound();
        }

        private async Task<IActionResult> SecurityResultAsync(User user, string? returnSurface, AccountActionResult result, string? postedEmail = null)
        {
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Error;
                ModelState.Clear(); // Never render submitted passwords or one-time codes again.
                ViewData["EmailChangeInput"] = postedEmail;
                var page = await _profiles.BuildAsync(user.UserId, Request.IsHttps, "security", null);
                return View("~/Views/Account/Profile.cshtml", page);
            }

            if (result.ReissueSession)
            {
                await _context.Entry(user).ReloadAsync();
                await SignInUserAsync(user);
            }

            TempData["SuccessMessage"] = result.Message;
            return returnSurface == "profile"
                ? RedirectToAction("Profile", new { tab = "security" })
                : RedirectToAction("Settings");
        }

        /// <summary>
        /// Structural gate only. Prefix/length can still fail at Verify (bad cost/salt) — callers must catch.
        /// </summary>
        private static bool LooksLikeLocalBcryptPasswordHash(string? passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                return false;
            }

            if (passwordHash.StartsWith("SOCIAL_LOGIN_", StringComparison.Ordinal))
            {
                return false;
            }

            // BCrypt modular crypt: $2a$ / $2b$ / $2y$ + cost + 22-char salt + 31-char checksum ≈ 59–60 chars.
            if (passwordHash.Length is < 59 or > 60)
            {
                return false;
            }

            return passwordHash.StartsWith("$2a$", StringComparison.Ordinal)
                || passwordHash.StartsWith("$2b$", StringComparison.Ordinal)
                || passwordHash.StartsWith("$2y$", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns false when hash is not verifiable (social/malformed/parse error). Never throws to MVC.
        /// </summary>
        private static bool TryVerifyLocalPassword(string password, string? passwordHash, out bool verified)
        {
            verified = false;
            if (!LooksLikeLocalBcryptPasswordHash(passwordHash))
            {
                return false;
            }

            try
            {
                verified = BCrypt.Net.BCrypt.Verify(password, passwordHash!);
                return true;
            }
            catch (SaltParseException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                // Invalid work factor / argument rejected by BCrypt.Net
                return false;
            }
        }

        private static bool IsValidEmailAddress(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                var parsed = new System.Net.Mail.MailAddress(email.Trim());
                return string.Equals(parsed.Address, email.Trim(), StringComparison.OrdinalIgnoreCase)
                    && email.Contains('@', StringComparison.Ordinal)
                    && parsed.Host.Contains('.', StringComparison.Ordinal);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
