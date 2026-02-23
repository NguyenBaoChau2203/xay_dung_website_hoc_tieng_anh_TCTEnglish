using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace TCTVocabulary.Controllers
{
    public class AccountController : Controller
    {
        private readonly DbflashcardContext _context;

        public AccountController(DbflashcardContext context)
        {
            _context = context;
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
                Role = "User" 
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
                    Role = "User",
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
                new Claim(ClaimTypes.Role, user.Role ?? "User")
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
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                ViewBag.Message = "Nếu email này tồn tại trong hệ thống, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu.";
                return View("ForgotPasswordConfirmation");
            }
            return View(model);
        }
    }
}