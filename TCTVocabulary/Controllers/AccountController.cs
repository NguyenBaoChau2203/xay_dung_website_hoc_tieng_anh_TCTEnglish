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

            // 5. Auto login
            await SignInUserAsync(newUser);

            return RedirectToAction("Index", "Home");
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            ViewData["ActiveTab"] = "login";

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                ViewBag.ErrorMessage = "Email hoặc mật khẩu không đúng.";
                return View("Auth");
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            if (!isPasswordValid)
            {
                ViewBag.ErrorMessage = "Email hoặc mật khẩu không đúng.";
                return View("Auth");
            }

            await SignInUserAsync(user);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // --- SOCIAL LOGIN ---

        // GET: /Account/ExternalLogin
        public IActionResult ExternalLogin(string provider)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        // GET: /Account/ExternalLoginCallback
        public async Task<IActionResult> ExternalLoginCallback()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // If checking cookie fail, try reading external info
            // Note: Usually Challenge redirects back and signs in to an external cookie schema, 
            // then we read it. But with minimal setup, let's see what info we get.
            // Actually, we usually AuthenticateAsync with the external scheme or default if set.
            
            // However, simpler pattern:
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme); 
            // Wait, we probably haven't signed into Cookie yet. We need to read from the provider.
            // When using AddGoogle, the default callback handles the exchange and signs in 'External' persistence if configured,
            // or we manually handle it.
            
            // Simplest way: Check ClaimsPrincipal from HttpContext.User if default scheme handled it?
            // No, strictly we should use:
            // var info = await HttpContext.AuthenticateAsync("Google"); // But we don't know which one.
            
            // Better approach:
            // The default callback path for Google is /signin-google. The middleware handles it.
            // BUT we used Challenge with a RedirectUri to THIS method.
            // So we need to retrieve the user info here.
            
            // Actually, the Challenge sets the return URL. The External Provider calls /signin-google, which does the handshake,
            // then the GoogleHandler redirects to our `RedirectUri` (ExternalLoginCallback).
            // At this point, the user IS authenticated in the *ExternalCookie* usually, OR we have the claims in context if we set it up right.
            
            // A helper to get external login info:
            // We usually can just call HttpContext.AuthenticateAsync() if we don't know the scheme,
            // but standard is to use a temporary scheme or just check User.Identity.
            
            // The issue: AddGoogle by default uses "Cookies" if DefaultSignInScheme is Cookies.
            // So `User` should already be populated with Google claims!
            
            if (!User.Identity!.IsAuthenticated)
            {
                 // Check if we have specific provider info?
                 // Sometimes it needs: await HttpContext.AuthenticateAsync("Google");
                 // Let's assume standard behavior: User is authenticated via Cookie (transiently) or we need to extract claims.
                 return RedirectToAction("Login");
            }

            // Extract info
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var nameClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(emailClaim))
            {
                ViewBag.ErrorMessage = "Không thể lấy thông tin Email từ tài khoản xã hội.";
                return View("Auth");
            }

            // Check database
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == emailClaim);
            if (user == null)
            {
                // Auto Register
                user = new User
                {
                    Email = emailClaim,
                    FullName = nameClaim ?? "Social User",
                    PasswordHash = "SOCIAL_LOGIN_" + Guid.NewGuid().ToString(), // Dummy password
                    CreatedAt = DateTime.Now,
                    Role = "User"
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            // Sign In with our App's Claims (consistency)
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