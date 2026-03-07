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
using System.Net;
using System.Net.Mail;

namespace TCTVocabulary.Controllers
{
    public class AccountController : Controller
    {
        private readonly DbflashcardContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;

        public AccountController(DbflashcardContext context, IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
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
                Role = Roles.Student
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

            await SignInUserAsync(user);
            
            // Return JSON success
            return Json(new { success = true });
        }

        // GET: /Account/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Clear any external authentication cookies to prevent auto-reuse
            // Delete common external cookie names
            foreach (var cookie in HttpContext.Request.Cookies.Keys)
            {
                if (cookie.Contains("External") || cookie.Contains("Correlation"))
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
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

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
                    Role = Roles.Student,
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
            }

            // 5. Sign in with our app's cookie claims
            await SignInUserAsync(user);

            return RedirectToAction("Index", "Home");
        }

        private async Task SignInUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? Roles.Student)
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

            // Generic message to prevent email enumeration
            var genericMessage = "Nếu email này tồn tại trong hệ thống, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu.";

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                // Show error: email not found
                ModelState.AddModelError("Email", "Email này không tồn tại trong hệ thống.");
                return View(model);
            }

            // Generate secure token and set expiry (15 minutes)
            var token = Guid.NewGuid().ToString();
            user.ResetPasswordToken = token;
            user.ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            // Build the reset link
            var resetLink = Url.Action("ResetPassword", "Account",
                new { token = token, email = user.Email }, Request.Scheme);

            // --- Send reset email via Gmail SMTP ---
            try
            {
                var smtpHost = _configuration["SmtpSettings:Host"];
                var smtpPort = int.Parse(_configuration["SmtpSettings:Port"] ?? "587");
                var senderEmail = _configuration["SmtpSettings:SenderEmail"];
                var senderName = _configuration["SmtpSettings:SenderName"] ?? "TCT English";
                var smtpPassword = _configuration["SmtpSettings:Password"];

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail!, senderName),
                    Subject = "Đặt lại mật khẩu - TCT English",
                    IsBodyHtml = true,
                    Body = $@"
                        <div style='font-family: Inter, Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 40px; background-color: #f9fafe;'>
                            <div style='background-color: #ffffff; padding: 40px; border-radius: 16px; box-shadow: 0 4px 12px rgba(0,0,0,0.05);'>
                                <h2 style='font-family: Montserrat, sans-serif; color: #2e3856; font-size: 24px; margin-bottom: 16px;'>Đặt lại mật khẩu</h2>
                                <p style='color: #586380; font-size: 16px; line-height: 1.6; margin-bottom: 24px;'>
                                    Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. 
                                    Nhấp vào nút bên dưới để tạo mật khẩu mới. Liên kết có hiệu lực trong <strong>15 phút</strong>.
                                </p>
                                <a href='{resetLink}' style='display: inline-block; padding: 14px 32px; background-color: #4255ff; color: #ffffff; text-decoration: none; border-radius: 8px; font-weight: 700; font-size: 16px;'>Đặt lại mật khẩu</a>
                                <p style='color: #939bb4; font-size: 13px; margin-top: 24px; line-height: 1.5;'>
                                    Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.
                                </p>
                            </div>
                        </div>"
                };
                mailMessage.To.Add(user.Email);

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(senderEmail, smtpPassword),
                    EnableSsl = true
                };
                await smtpClient.SendMailAsync(mailMessage);

                Console.WriteLine($"[Email] Reset password email sent to {user.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email Error]: {ex.Message}");
                // Still show confirmation even if email fails
            }

            ViewBag.Message = genericMessage;
            return View("ForgotPasswordConfirmation");
        }

        // GET: /Account/Profile
        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

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
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

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

                    // Ensure directory exists
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.Avatar.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Avatar.CopyToAsync(fileStream);
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
                _context.Users.Update(user);
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
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

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
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

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
                _context.Users.Update(user);
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
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

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
            _context.Users.Update(user);
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