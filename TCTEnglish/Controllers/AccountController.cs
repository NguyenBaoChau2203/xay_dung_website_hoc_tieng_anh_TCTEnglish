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
        private readonly IAvatarUploadService _avatarUploadService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(DbflashcardContext context, IWebHostEnvironment webHostEnvironment, IAppEmailSender emailSender, IAvatarUploadService avatarUploadService, ILogger<AccountController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _emailSender = emailSender;
            _avatarUploadService = avatarUploadService;
            _logger = logger;
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

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return RedirectToAction("Login");

            var savedWordsCount = await _context.Cards
                .AsNoTracking()
                .Where(c => c.Set.OwnerId == userId)
                .CountAsync();

            var model = new TCTVocabulary.ViewModels.UpdateProfileViewModel
            {
                FullName = user.FullName ?? string.Empty,
                CurrentAvatarUrl = user.AvatarUrl,
                StreakDays = user.Streak ?? 0,
                SavedWordsCount = savedWordsCount
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
                    try
                    {
                        user.AvatarUrl = await _avatarUploadService.UploadAvatarAsync(model.Avatar, user.AvatarUrl);
                    }
                    catch (InvalidOperationException ex)
                    {
                        TempData["ErrorMessage"] = ex.Message;
                        model.CurrentAvatarUrl = user.AvatarUrl;
                        model.StreakDays = user.Streak ?? 0;
                        model.SavedWordsCount = await _context.Cards
                            .AsNoTracking()
                            .Where(c => c.Set.OwnerId == userId)
                            .CountAsync();
                        return View("~/Views/Account/Profile.cshtml", model);
                    }
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

        // POST: /Account/DeleteAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> DeleteAccount()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login");

            var accountDeleted = false;

            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user == null)
                    {
                        return;
                    }

                    await DeleteUserRelatedDataAsync(userId);

                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                    accountDeleted = true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete account for User {UserId}", userId);
                TempData["ErrorMessage"] = "Unable to delete account at this time. Please try again.";
                return RedirectToAction("Settings");
            }

            if (!accountDeleted) return NotFound();

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            try { await HttpContext.SignOutAsync("ExternalCookie"); } catch { }

            TempData["SuccessMessage"] = "Your account has been deleted permanently.";
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

        private async Task DeleteUserRelatedDataAsync(int userId)
        {
            var classFoldersAdded = await _context.ClassFolders
                .Where(cf => cf.AddedByUserId == userId)
                .ToListAsync();
            if (classFoldersAdded.Count > 0)
            {
                _context.ClassFolders.RemoveRange(classFoldersAdded);
            }

            var classMessages = await _context.ClassMessages
                .Where(cm => cm.UserId == userId)
                .ToListAsync();
            if (classMessages.Count > 0)
            {
                _context.ClassMessages.RemoveRange(classMessages);
            }

            var classMembers = await _context.ClassMembers
                .Where(cm => cm.UserId == userId)
                .ToListAsync();
            if (classMembers.Count > 0)
            {
                _context.ClassMembers.RemoveRange(classMembers);
            }

            var savedFolders = await _context.SavedFolders
                .Where(sf => sf.UserId == userId)
                .ToListAsync();
            if (savedFolders.Count > 0)
            {
                _context.SavedFolders.RemoveRange(savedFolders);
            }

            var userLearningProgresses = await _context.LearningProgresses
                .Where(lp => lp.UserId == userId)
                .ToListAsync();
            if (userLearningProgresses.Count > 0)
            {
                _context.LearningProgresses.RemoveRange(userLearningProgresses);
            }

            var speakingProgresses = await _context.UserSpeakingProgresses
                .Where(p => p.UserId == userId)
                .ToListAsync();
            if (speakingProgresses.Count > 0)
            {
                _context.UserSpeakingProgresses.RemoveRange(speakingProgresses);
            }

            var ownedClasses = await _context.Classes
                .Where(c => c.OwnerId == userId)
                .ToListAsync();
            if (ownedClasses.Count > 0)
            {
                _context.Classes.RemoveRange(ownedClasses);
            }

            await DeleteOwnedLearningContentAsync(userId);
        }

        private async Task DeleteOwnedLearningContentAsync(int userId)
        {
            var ownedSetIds = await _context.Sets
                .AsNoTracking()
                .Where(s => s.OwnerId == userId)
                .Select(s => s.SetId)
                .ToListAsync();

            if (ownedSetIds.Count > 0)
            {
                var ownedCardIds = await _context.Cards
                    .AsNoTracking()
                    .Where(c => ownedSetIds.Contains(c.SetId))
                    .Select(c => c.CardId)
                    .ToListAsync();

                if (ownedCardIds.Count > 0)
                {
                    var progressesForOwnedCards = await _context.LearningProgresses
                        .Where(lp => ownedCardIds.Contains(lp.CardId) && lp.UserId != userId)
                        .ToListAsync();
                    if (progressesForOwnedCards.Count > 0)
                    {
                        _context.LearningProgresses.RemoveRange(progressesForOwnedCards);
                    }

                    var ownedCards = await _context.Cards
                        .Where(c => ownedCardIds.Contains(c.CardId))
                        .ToListAsync();
                    if (ownedCards.Count > 0)
                    {
                        _context.Cards.RemoveRange(ownedCards);
                    }
                }

                var ownedSets = await _context.Sets
                    .Where(s => ownedSetIds.Contains(s.SetId))
                    .ToListAsync();
                if (ownedSets.Count > 0)
                {
                    _context.Sets.RemoveRange(ownedSets);
                }
            }

            var ownedFolderIds = await _context.Folders
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => f.FolderId)
                .ToListAsync();

            if (ownedFolderIds.Count > 0)
            {
                var savedFolderReferences = await _context.SavedFolders
                    .Where(sf => ownedFolderIds.Contains(sf.FolderId))
                    .ToListAsync();
                if (savedFolderReferences.Count > 0)
                {
                    _context.SavedFolders.RemoveRange(savedFolderReferences);
                }

                await DeleteOwnedFoldersAsync(userId);
            }
        }

        private async Task DeleteOwnedFoldersAsync(int userId)
        {
            var folders = await _context.Folders
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => new { f.FolderId, f.ParentFolderId })
                .ToListAsync();

            if (folders.Count == 0)
            {
                return;
            }

            var folderMap = folders.ToDictionary(f => f.FolderId, f => f.ParentFolderId);
            var depthCache = new Dictionary<int, int>();

            int GetDepth(int folderId)
            {
                if (depthCache.TryGetValue(folderId, out var cachedDepth))
                {
                    return cachedDepth;
                }

                if (!folderMap.TryGetValue(folderId, out var parentId)
                    || parentId is null
                    || !folderMap.ContainsKey(parentId.Value))
                {
                    depthCache[folderId] = 0;
                    return 0;
                }

                var depth = GetDepth(parentId.Value) + 1;
                depthCache[folderId] = depth;
                return depth;
            }

            var orderedFolderIds = folderMap.Keys
                .OrderByDescending(GetDepth)
                .ToList();

            var trackedFolders = await _context.Folders
                .Where(f => orderedFolderIds.Contains(f.FolderId))
                .ToListAsync();

            var trackedFolderLookup = trackedFolders.ToDictionary(f => f.FolderId);
            foreach (var folderId in orderedFolderIds)
            {
                if (trackedFolderLookup.TryGetValue(folderId, out var folder))
                {
                    _context.Folders.Remove(folder);
                }
            }
        }
    }
}
