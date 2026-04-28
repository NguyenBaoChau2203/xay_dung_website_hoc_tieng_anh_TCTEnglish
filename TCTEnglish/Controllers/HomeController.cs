using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class HomeController : BaseController
    {
        private static readonly TimeSpan DailyChallengeTokenLifetime = TimeSpan.FromMinutes(15);
        private const int DailyChallengeXp = 10;
        private readonly DbflashcardContext _context;
        private readonly IAppEmailSender _emailSender;
        private readonly IGoalsService _goalsService;
        private readonly ILogger<HomeController> _logger;
        private readonly IDataProtector _dailyChallengeProtector;

        public HomeController(
            DbflashcardContext context,
            IAppEmailSender emailSender,
            IGoalsService goalsService,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<HomeController> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _goalsService = goalsService;
            _logger = logger;
            _dailyChallengeProtector = dataProtectionProvider.CreateProtector("HomeController.DailyChallenge.v1");
        }

        public async Task<IActionResult> Index()
        {
            if (!User.Identity!.IsAuthenticated)
            {
                return RedirectToAction(nameof(Landing));
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await BuildDashboardViewModelAsync(userId);
            return model == null
                ? RedirectToAction("Login", "Account")
                : View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetDailyChallenge()
        {
            var challenge = await GetRandomChallengeAsync();
            return PartialView("_DailyChallenge", challenge);
        }

        private async Task<DashboardViewModel?> BuildDashboardViewModelAsync(int userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .AsSplitQuery()
                .Include(u => u.Folders)
                    .ThenInclude(f => f.Sets)
                .Include(u => u.Sets)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return null;
            }

            var setIds = user.Sets.Select(s => s.SetId).ToList();

            var cardCount = await _context.Cards
                .AsNoTracking()
                .CountAsync(c => setIds.Contains(c.SetId));

            var todayFolders = await GetTodayFoldersAsync(userId);

            // --- LOGIC THÊM VÀO: Lấy bài đọc đang học dở gần nhất ---
            var recentReading = await _context.UserReadingHistories
                .AsNoTracking()
                .Include(h => h.ReadingPassage)
                .Where(h => h.UserId == userId && h.IsCompleted == false)
                .OrderByDescending(h => h.ViewedAt)
                .Select(h => new RecentReadingViewModel
                {
                    Id = h.ReadingPassageId,
                    Title = h.ReadingPassage.Title,
                    Level = h.ReadingPassage.Level,
                    Topic = h.ReadingPassage.Topic,
                    LastViewed = h.ViewedAt
                })
                .FirstOrDefaultAsync();
            // -------------------------------------------------------

            return new DashboardViewModel
            {
                FullName = user.FullName,
                Streak = user.Streak ?? 0,
                Goal = user.Goal ?? 0,
                FolderCount = user.Folders.Count,
                SetCount = user.Sets.Count,
                CardCount = cardCount,
                DailyChallenge = await GetRandomChallengeAsync(),
                TodayFolders = todayFolders,
                // Gán dữ liệu vào ViewModel
                RecentInProcessReading = recentReading
            };
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckAnswer(int selectedCardId, string challengeToken)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                _logger.LogWarning("Unauthorized check answer request for selected card {cardId}", selectedCardId);
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(challengeToken)
                || !TryReadDailyChallengeAnswer(challengeToken, userId, out var correctCardId))
            {
                _logger.LogWarning(
                    "Invalid daily challenge token for user {userId}, selected card {cardId}",
                    userId,
                    selectedCardId);
                return BadRequest();
            }

            var isCorrect = selectedCardId == correctCardId;

            if (isCorrect)
            {
                var activityUpdate = new GoalsActivityUpdate
                {
                    CardsReviewed = 1,
                    QuizzesCompleted = 1,
                    XpEarned = DailyChallengeXp
                };

                var activityResult = await _goalsService.RecordActivityAsync(userId, activityUpdate);
                if (activityResult.Status == OperationStatus.NotFound)
                {
                    _logger.LogWarning("Daily challenge activity record skipped because user {userId} was not found", userId);
                    return NotFound();
                }

                if (activityResult.Status == OperationStatus.Invalid)
                {
                    _logger.LogWarning("Daily challenge activity record rejected for user {userId}", userId);
                    return BadRequest();
                }

                await _goalsService.UpdateStreakAndRewardsAsync(userId);
                _logger.LogInformation("Correct answer recorded for user {userId}, card {cardId}", userId, correctCardId);
            }

            return Json(new { correct = isCorrect, correctCardId });
        }

        // 1. Cập nhật hàm lấy Challenge ngẫu nhiên
        private async Task<DailyChallengeViewModel> GetRandomChallengeAsync()
        {
            // Lọc Cards: Card -> thuộc Set -> có Owner (User) -> có Role là "System"
            var systemCardsQuery = _context.Cards
                .AsNoTracking()
                .Where(c => c.Set.Owner.Role == "System"); // Lọc theo Role System

            var totalCards = await systemCardsQuery.CountAsync();

            if (totalCards == 0) return new DailyChallengeViewModel();

            var randomIndex = Random.Shared.Next(totalCards);
            var randomCard = await systemCardsQuery
                .Skip(randomIndex)
                .FirstOrDefaultAsync();

            if (randomCard == null) return new DailyChallengeViewModel();

            // Lấy các phương án sai cũng chỉ từ nguồn System
            var wrongAnswers = await GetSystemWrongAnswersAsync(randomCard.CardId);

            wrongAnswers.Add(new AnswerOption
            {
                CardId = randomCard.CardId,
                Definition = randomCard.Definition
            });

            var challengeToken = string.Empty;
            if (TryGetCurrentUserId(out var userId))
            {
                challengeToken = CreateDailyChallengeToken(userId, randomCard.CardId);
            }

            return new DailyChallengeViewModel
            {
                CardId = randomCard.CardId,
                Term = randomCard.Term,
                ChallengeToken = challengeToken,
                Options = wrongAnswers.OrderBy(_ => Guid.NewGuid()).ToList()
            };
        }

        private string CreateDailyChallengeToken(int userId, int correctCardId)
        {
            var expiresAtTicks = DateTime.UtcNow.Add(DailyChallengeTokenLifetime).Ticks;
            var payload = $"{userId}|{correctCardId}|{expiresAtTicks}";
            var protectedPayload = _dailyChallengeProtector.Protect(payload);
            return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protectedPayload));
        }

        private bool TryReadDailyChallengeAnswer(string challengeToken, int userId, out int correctCardId)
        {
            correctCardId = 0;

            try
            {
                var protectedPayload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(challengeToken));
                var payload = _dailyChallengeProtector.Unprotect(protectedPayload);
                var segments = payload.Split('|', StringSplitOptions.None);
                if (segments.Length != 3)
                {
                    return false;
                }

                if (!int.TryParse(segments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var tokenUserId)
                    || tokenUserId != userId)
                {
                    return false;
                }

                if (!int.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var tokenCorrectCardId))
                {
                    return false;
                }

                if (!long.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAtTicks)
                    || expiresAtTicks < DateTime.MinValue.Ticks
                    || expiresAtTicks > DateTime.MaxValue.Ticks)
                {
                    return false;
                }

                if (new DateTime(expiresAtTicks, DateTimeKind.Utc) < DateTime.UtcNow)
                {
                    return false;
                }

                correctCardId = tokenCorrectCardId;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        private async Task<List<TodayFolderViewModel>> GetTodayFoldersAsync(int userId)
        {
            var eligibleFolders = _context.Folders
                .AsNoTracking()
                .Where(f => f.UserId != userId);

            if (_context.Database.IsSqlServer())
            {
                return await eligibleFolders
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .Select(f => new TodayFolderViewModel
                    {
                        FolderId = f.FolderId,
                        FolderName = f.FolderName,
                        SetCount = f.Sets.Count,
                        CreatorName = f.User.FullName ?? "Unknown"
                    })
                    .ToListAsync();
            }

            // FIX: SQLite test provider cannot translate Guid.NewGuid() inside OrderBy.
            var folderIds = await TakeRandomIdsAsync(
                eligibleFolders
                    .OrderBy(f => f.FolderId)
                    .Select(f => f.FolderId),
                3);

            if (folderIds.Count == 0)
            {
                return new List<TodayFolderViewModel>();
            }

            var folders = await eligibleFolders
                .Where(f => folderIds.Contains(f.FolderId))
                .Select(f => new TodayFolderViewModel
                {
                    FolderId = f.FolderId,
                    FolderName = f.FolderName,
                    SetCount = f.Sets.Count,
                    CreatorName = f.User.FullName ?? "Unknown"
                })
                .ToListAsync();

            var foldersById = folders.ToDictionary(f => f.FolderId);
            return folderIds
                .Where(foldersById.ContainsKey)
                .Select(id => foldersById[id])
                .ToList();
        }

        // 2. Cập nhật hàm lấy phương án sai
        private async Task<List<AnswerOption>> GetSystemWrongAnswersAsync(int excludedCardId)
        {
            var eligibleCards = _context.Cards
                .AsNoTracking()
                .Where(c => c.CardId != excludedCardId && c.Set.Owner.Role == "System");

            // Lấy ngẫu nhiên 3 định nghĩa từ System
            if (_context.Database.IsSqlServer())
            {
                return await eligibleCards
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .Select(c => new AnswerOption
                    {
                        CardId = c.CardId,
                        Definition = c.Definition
                    })
                    .ToListAsync();
            }

            var randomCardIds = await TakeRandomIdsAsync(
                eligibleCards
                    .OrderBy(c => c.CardId)
                    .Select(c => c.CardId),
                3);

            if (randomCardIds.Count == 0)
            {
                return new List<AnswerOption>();
            }

            var options = await eligibleCards
                .Where(c => randomCardIds.Contains(c.CardId))
                .Select(c => new AnswerOption
                {
                    CardId = c.CardId,
                    Definition = c.Definition
                })
                .ToListAsync();

            var optionsById = options.ToDictionary(x => x.CardId);
            return randomCardIds
                .Where(optionsById.ContainsKey)
                .Select(id => optionsById[id])
                .ToList();
        }

        // 3. Thêm Action mới để trả về Partial View cho AJAX
        [HttpGet]
        public async Task<IActionResult> RefreshDailyChallenge()
        {
            var challenge = await GetRandomChallengeAsync();
            return PartialView("_DailyChallenge", challenge);
        }

        private static List<int> GetUniqueRandomOffsets(int totalCount, int takeCount)
        {
            var targetCount = Math.Min(totalCount, takeCount);
            var offsets = new List<int>(targetCount);
            var seenOffsets = new HashSet<int>();

            while (offsets.Count < targetCount)
            {
                var offset = Random.Shared.Next(totalCount);
                if (seenOffsets.Add(offset))
                {
                    offsets.Add(offset);
                }
            }

            return offsets;
        }

        private async Task<List<int>> TakeRandomIdsAsync(IQueryable<int> orderedIdsQuery, int takeCount)
        {
            var totalCount = await orderedIdsQuery.CountAsync();
            if (totalCount == 0)
            {
                return new List<int>();
            }

            if (totalCount <= takeCount)
            {
                var allIds = await orderedIdsQuery.ToListAsync();
                return allIds.OrderBy(_ => Random.Shared.Next()).ToList();
            }

            var randomOffsets = GetUniqueRandomOffsets(totalCount, takeCount);
            var randomIds = new List<int>(randomOffsets.Count);

            foreach (var offset in randomOffsets)
            {
                randomIds.Add(await orderedIdsQuery.Skip(offset).FirstAsync());
            }

            return randomIds;
        }

        [AllowAnonymous]
        public IActionResult Landing()
        {
            return View();
        }
        public IActionResult Introduction()
        {
            return View("~/Views/Footer/Introduction.cshtml");
        }

        // Trang Điều khoản sử dụng
        public IActionResult Termsofuse()
        {
            return View("~/Views/Footer/Termsofuse.cshtml");
        }

        // Trang Chính sách bảo mật
        public IActionResult Privacypolicy()
        {
            return View("~/Views/Footer/Privacypolicy.cshtml");
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Contact(string? subject)
        {
            var vm = new ContactFormViewModel
            {
                Subject = ContactFormViewModel.NormalizeSubject(subject)
            };

            return View(vm);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactFormViewModel model)
        {
            model.Subject = ContactFormViewModel.NormalizeSubject(model.Subject);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var isSent = await _emailSender.SendContactMessageAsync(
                model.Name,
                model.Email,
                model.SubjectDisplayName,
                model.Message,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());

            TempData[isSent ? "ContactSuccessMessage" : "ContactErrorMessage"] = isSent
                ? "Yêu cầu của bạn đã được gửi. Chúng tôi sẽ phản hồi trong thời gian sớm nhất."
                : "Không thể gửi liên hệ lúc này. Vui lòng thử lại sau hoặc email support.tctenglish@gmail.com.";

            return RedirectToAction(nameof(Contact), new { subject = model.Subject });
        }

        [HttpGet]
        public async Task<IActionResult> Search(string? q)
        {
            var vm = new SearchViewModel
            {
                Query = q?.Trim() ?? string.Empty
            };

            if (!string.IsNullOrWhiteSpace(vm.Query))
            {
                var query = vm.Query;

                vm.Folders = await _context.Folders
                    .AsNoTracking()
                    .Where(f => f.FolderName.Contains(query))
                    .OrderBy(f => f.FolderName)
                    .Select(f => new FolderSearchResultViewModel
                    {
                        FolderId = f.FolderId,
                        FolderName = f.FolderName,
                        CreatorName = f.User.FullName ?? "Người dùng"
                    })
                    .ToListAsync();

                vm.Classes = await _context.Classes
                    .AsNoTracking()
                    .Where(c => c.ClassName.Contains(query))
                    .OrderBy(c => c.ClassName)
                    .Select(c => new SearchClassResultViewModel
                    {
                        ClassId = c.ClassId,
                        ClassName = c.ClassName,
                        OwnerName = c.Owner.FullName ?? "Người dùng"
                    })
                    .ToListAsync();

                vm.Users = await _context.Users
                    .AsNoTracking()
                    .Where(u =>
                        (u.FullName != null && u.FullName.Contains(query))
                        || (u.Email != null && u.Email.Contains(query)))
                    .OrderBy(u => u.FullName ?? u.Email ?? string.Empty)
                    .Select(u => new UserSearchResultViewModel
                    {
                        UserId = u.UserId,
                        FullName = u.FullName ?? u.Email ?? "Người dùng",
                        Email = u.Email ?? string.Empty,
                        AvatarUrl = u.AvatarUrl
                    })
                    .ToListAsync();
            }

            return View(vm);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult Login()
        {
            return RedirectToAction("Login", "Account");
        }

        public IActionResult Register()
        {
            return RedirectToAction("Register", "Account");
        }
    }
}
