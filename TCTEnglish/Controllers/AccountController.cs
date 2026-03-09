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
using System.IO;
using TCTVocabulary.Services;

namespace TCTVocabulary.Controllers
{
    public class AccountController : Controller
    {
        private readonly DbflashcardContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IAppEmailSender _emailSender;

        public AccountController(DbflashcardContext context, IWebHostEnvironment webHostEnvironment, IAppEmailSender emailSender)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _emailSender = emailSender;
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

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string email, string password, string fullName) // Mapping Username input to FullName
        {
            ViewData["ActiveTab"] = "register"; // Keep user on Register tab if error

            // 1. Validate inputs
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.ErrorMessage = "Vui lòng nhập đầy đủ thông tin.";
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
                CreatedAt = DateTime.Now,
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
            if (user == null)
            {
                // Return JSON error
                return Json(new { success = false, message = "Email hoặc mật khẩu không đúng." });
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!isPasswordValid)
            {
                // Return JSON error
                return Json(new { success = false, message = "Email hoặc mật khẩu không đúng." });
            }

            // --- Block / Auto-Unlock check ---
            if (user.Status == UserStatus.Blocked)
            {
                // Lazy Auto-Unlock: if lock has expired, reset and allow login
                if (user.LockExpiry.HasValue && DateTime.UtcNow >= user.LockExpiry.Value)
                {
                    user.Status = UserStatus.Offline;
                    user.LockReason = null;
                    user.LockExpiry = null;
                    await _context.SaveChangesAsync();

                    await _emailSender.SendUnlockedNotificationAsync(user.Email, isAutoUnlock: true);
                    // Fall through to sign-in below
                }
                else
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
                    CreatedAt = DateTime.Now,
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

        private async Task SignInUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? Roles.Standard)
            };

            // Add AvatarUrl Claim if exists
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                claims.Add(new Claim("AvatarUrl", user.AvatarUrl));
            }

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

            // SECURE: Generic message regardless of outcome to prevent email enumeration attacks
            var genericMessage = "Nếu email này tồn tại trong hệ thống, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu.";

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                // SECURE: Do NOT reveal whether the email exists — show the same generic message
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
        public async Task<IActionResult> Profile()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login");

            // OPTIMIZE: AsNoTracking — read-only query for display
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var model = new TCTVocabulary.ViewModels.UpdateProfileViewModel
            {
                FullName = user.FullName ?? string.Empty,
                CurrentAvatarUrl = user.AvatarUrl
            };

            return View("~/Views/Account/Profile.cshtml", model);
        }

        // POST: /Account/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UpdateProfile(TCTVocabulary.ViewModels.UpdateProfileViewModel model)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return RedirectToAction("Login");
            
            if (ModelState.IsValid)
            {
                // Upload Avatar
                if (model.Avatar != null)
                {
                    // Validate file size (2MB max)
                    if (model.Avatar.Length > 2 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File ảnh không được vượt quá 2MB.";
                        model.CurrentAvatarUrl = user.AvatarUrl;
                        return View("~/Views/Account/Profile.cshtml", model);
                    }

                    // Validate extension
                    var ext = Path.GetExtension(model.Avatar.FileName).ToLowerInvariant();
                    if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                    {
                        TempData["ErrorMessage"] = "Chỉ chấp nhận định dạng ảnh .jpg, .jpeg, .png";
                        model.CurrentAvatarUrl = user.AvatarUrl;
                        return View("~/Views/Account/Profile.cshtml", model);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.Avatar.FileName;
                    string filePath = "";
                    using (var ms = new MemoryStream())
                    {
                        await model.Avatar.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        bool isImage = false;
                        
                        if (fileBytes.Length >= 3 && fileBytes[0] == 0xFF && fileBytes[1] == 0xD8 && fileBytes[2] == 0xFF)
                            isImage = true;
                        else if (fileBytes.Length >= 8 && fileBytes[0] == 0x89 && fileBytes[1] == 0x50 && fileBytes[2] == 0x4E && fileBytes[3] == 0x47 &&
                                 fileBytes[4] == 0x0D && fileBytes[5] == 0x0A && fileBytes[6] == 0x1A && fileBytes[7] == 0x0A)
                            isImage = true;

                        if (!isImage)
                        {
                            TempData["ErrorMessage"] = "Tệp tải lên không phải là định dạng hình ảnh hợp lệ (Nguy cơ bảo mật).";
                            model.CurrentAvatarUrl = user.AvatarUrl;
                            return View("~/Views/Account/Profile.cshtml", model);
                        }

                        // Ensure directory exists
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                        Directory.CreateDirectory(uploadsFolder);
                        filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);
                    }

                    // Delete old avatar if it's a local file
                    if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl.StartsWith("/images/avatars/"))
                    {
                        string oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, user.AvatarUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    user.AvatarUrl = "/images/avatars/" + uniqueFileName;
                }

                user.FullName = model.FullName;
                // FIX: Removed redundant _context.Users.Update(user) — EF Core tracks fetched entity automatically
                await _context.SaveChangesAsync();

                // Refresh cookies to update claims (like Name, AvatarUrl)
                await SignInUserAsync(user);

                TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công.";
            }

            return RedirectToAction("Profile");
        }

        // GET: /Account/Settings
        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login");

            // OPTIMIZE: AsNoTracking — read-only query for display
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var model = new TCTVocabulary.ViewModels.SecuritySettingsViewModel
            {
                Email = user.Email
            };

            return View("~/Views/Account/Settings.cshtml", model);
        }

        // POST: /Account/UpdateEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UpdateEmail(TCTVocabulary.ViewModels.SecuritySettingsViewModel model)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return RedirectToAction("Login");

            if (string.IsNullOrEmpty(model.Email))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập Email.";
                return RedirectToAction("Settings");
            }

            if (model.Email != user.Email)
            {
                // Ensure new email doesn't exist
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (existingUser != null)
                {
                    TempData["ErrorMessage"] = "Email này đã được sử dụng bởi người dùng khác.";
                    return RedirectToAction("Settings");
                }
                user.Email = model.Email;
                // FIX: Removed redundant _context.Users.Update(user) — EF Core tracks fetched entity automatically
                await _context.SaveChangesAsync();

                // Refresh cookies
                await SignInUserAsync(user);

                TempData["SuccessMessage"] = "Cập nhật Email thành công.";
            }

            return RedirectToAction("Settings");
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ChangePassword(TCTVocabulary.ViewModels.SecuritySettingsViewModel model)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return RedirectToAction("Login");

            if (string.IsNullOrEmpty(model.NewPassword))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập Mật khẩu mới.";
                return RedirectToAction("Settings");
            }

            if (string.IsNullOrEmpty(model.CurrentPassword))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập Mật khẩu hiện tại để đổi mật khẩu.";
                return RedirectToAction("Settings");
            }

            if (!user.PasswordHash.StartsWith("SOCIAL_LOGIN_"))
            {
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash);
                if (!isPasswordValid)
                {
                    TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng.";
                    return RedirectToAction("Settings");
                }
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["ErrorMessage"] = "Mật khẩu xác nhận không khớp.";
                return RedirectToAction("Settings");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            // FIX: Removed redundant _context.Users.Update(user) — EF Core tracks fetched entity automatically
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật Mật khẩu thành công.";

            return RedirectToAction("Settings");
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

            // Find user matching both email and token
            var user = await _context.Users.FirstOrDefaultAsync(
                u => u.Email == model.Email && u.ResetPasswordToken == model.Token);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid token or email.");
                return View(model);
            }

            // Check if token has expired
            if (user.ResetPasswordTokenExpiry < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "Token has expired.");
                return View(model);
            }

            // Hash the new password using BCrypt (same as Register action)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            // Invalidate the token to prevent reuse
            user.ResetPasswordToken = null;
            user.ResetPasswordTokenExpiry = null;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Password reset successfully. Please log in.";
            return RedirectToAction("Login");
        }
    }
}