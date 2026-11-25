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
using TCTEnglish.Services.Contact;

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
        private readonly TCTEnglish.Services.Notifications.LearningBusinessClock _clock;
        private readonly IDataProtector _dailyChallengeProtector;

        public HomeController(
            DbflashcardContext context,
            IAppEmailSender emailSender,
            IGoalsService goalsService,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<HomeController> logger,
            TCTEnglish.Services.Notifications.LearningBusinessClock clock)
        {
            _context = context;
            _emailSender = emailSender;
            _goalsService = goalsService;
            _logger = logger;
            _clock = clock;
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
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var challenge = await GetDailyChallengeForUserAsync(userId);
            return PartialView("_DailyChallenge", challenge);
        }

        private async Task<DashboardViewModel?> BuildDashboardViewModelAsync(int userId)
        {
            using var operation = _clock.BeginOperation();
            var user = await _context.Users
                .AsNoTracking()
                .AsSplitQuery()
                .Include(u => u.Folders)
                    .ThenInclude(f => f.Sets)
                .Include(u => u.Sets)
                .FirstOrDefaultAsync(u => u.UserId == userId && u.AnonymizedAt == null);

            if (user == null)
            {
                return null;
            }

            var setIds = user.Sets.Select(s => s.SetId).ToList();

            var ownedCardIds = await _context.Cards
                .AsNoTracking()
                .Where(c => setIds.Contains(c.SetId))
                .Select(c => c.CardId)
                .ToListAsync();

            var cardCount = ownedCardIds.Count;

            var progressStatuses = await _context.LearningProgresses
                .AsNoTracking()
                .Where(lp => lp.UserId == userId && ownedCardIds.Contains(lp.CardId))
                .Select(lp => lp.Status)
                .ToListAsync();

            var masteredCount = progressStatuses.Count(status =>
                string.Equals(status, "Mastered", StringComparison.OrdinalIgnoreCase));
            var learningCount = progressStatuses.Count(status =>
                string.Equals(status, "Learning", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Reviewing", StringComparison.OrdinalIgnoreCase));
            var newCount = Math.Max(0, cardCount - masteredCount - learningCount);

            var heatmapCells = await BuildDashboardHeatmapAsync(userId);
            var today = _clock.BusinessDate;
            var goalsSnapshot = await _goalsService.GetGoalsAsync(userId);
            var xpTotals = await _context.UserDailyActivities.AsNoTracking()
                .Where(a => a.UserId == userId && a.ActivityDate <= today)
                .GroupBy(_ => 1).Select(g => new
                {
                    Total = g.Sum(a => a.XpEarned),
                    Today = g.Sum(a => a.ActivityDate == today ? a.XpEarned : 0)
                }).FirstOrDefaultAsync();

            var todayFolders = await GetTodayFoldersAsync(userId);

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
                    ImageUrl = h.ReadingPassage.ImageUrl,
                    LastViewed = h.ViewedAt
                })
                .FirstOrDefaultAsync();

            var cardsReviewedToday = await _context.UserDailyActivities
                .AsNoTracking()
                .Where(a => a.UserId == userId && a.ActivityDate == today)
                .Select(a => (int?)a.CardsReviewed)
                .FirstOrDefaultAsync() ?? 0;

            return new DashboardViewModel
            {
                FullName = user.FullName,
                Role = user.Role,
                Streak = TCTEnglish.Services.Learning.StudyDayProjection.CurrentStreak(user.LastStudyDate, user.Streak, _clock.BusinessDate),
                TotalXp = xpTotals?.Total ?? 0,
                TodayXp = xpTotals?.Today ?? 0,
                DailyGoalPercent = goalsSnapshot?.ProgressPercent ?? 0,
                ActiveGoalCount = goalsSnapshot?.GoalCards.Count(c => c.TargetValue > 0) ?? 0,
                MetGoalCount = goalsSnapshot?.GoalCards.Count(c => c.TargetValue > 0 && c.TodayProgressValue >= c.TargetValue) ?? 0,
                DailyGoalSummary = string.Join("; ", goalsSnapshot?.GoalCards.Where(c => c.TargetValue > 0)
                    .Select(c => $"{c.AreaLabel}: {c.TodayProgressValue}/{c.TargetValue} {c.UnitLabel}") ?? []),
                BusinessHour = _clock.UtcNow.AddHours(7).Hour,
                StudyDays = goalsSnapshot?.StudyDays ?? [],
                Goal = user.Goal ?? 0,
                FolderCount = user.Folders.Count,
                SetCount = user.Sets.Count,
                CardCount = cardCount,
                MasteredCount = masteredCount,
                LearningCount = learningCount,
                NewCount = newCount,
                CardsReviewedToday = cardsReviewedToday,
                DailyChallenge = await GetDailyChallengeForUserAsync(userId),
                TodayFolders = todayFolders,
                HeatmapCells = heatmapCells,
                FeatureTimeBreakdown = await BuildFeatureTimeBreakdownAsync(userId),
                RecentInProcessReading = recentReading
            };
        }

        private async Task<List<DashboardHeatmapCellViewModel>> BuildDashboardHeatmapAsync(int userId)
        {
            var today = _clock.BusinessDate;
            var daysSinceMonday = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysSinceMonday < 0)
            {
                daysSinceMonday += 7;
            }
            // Monday of the current week, then back 3 weeks → 28 Mon–Sun cells.
            var startDate = today.AddDays(-daysSinceMonday - 21);

            var activityByDate = await _context.UserDailyActivities
                .AsNoTracking()
                .Where(a => a.UserId == userId && a.ActivityDate >= startDate && a.ActivityDate <= today)
                .Select(a => new { a.ActivityDate, a.CardsReviewed })
                .ToListAsync();

            var activityByDateLookup = activityByDate
                .GroupBy(x => x.ActivityDate.Date)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.CardsReviewed));

            var cells = new List<DashboardHeatmapCellViewModel>(capacity: 28);
            for (var i = 0; i < 28; i++)
            {
                var date = startDate.AddDays(i);
                var weekIndex = (i / 7) + 1;
                var dayLabel = date.DayOfWeek switch
                {
                    DayOfWeek.Monday => "T2",
                    DayOfWeek.Tuesday => "T3",
                    DayOfWeek.Wednesday => "T4",
                    DayOfWeek.Thursday => "T5",
                    DayOfWeek.Friday => "T6",
                    DayOfWeek.Saturday => "T7",
                    _ => "CN"
                };

                cells.Add(new DashboardHeatmapCellViewModel
                {
                    WeekLabel = $"Tuần {weekIndex}",
                    DayLabel = dayLabel,
                    FullDate = date.ToString("dd/MM/yyyy"),
                    ReviewedCards = activityByDateLookup.TryGetValue(date, out var value) ? value : 0
                });
            }

            return cells;
        }

        /// <summary>
        /// Trả về thời gian học (phút) theo từng tính năng trong tuần hiện tại (Từ T2 đến CN).
        /// </summary>
        private async Task<List<DailyFeatureTimeViewModel>> BuildFeatureTimeBreakdownAsync(int userId)
        {
            var today = _clock.BusinessDate;
            int daysSinceMonday = (int)today.DayOfWeek - (int)DayOfWeek.Monday;
            if (daysSinceMonday < 0)
            {
                daysSinceMonday += 7;
            }
            var startDate = today.AddDays(-daysSinceMonday); // Thứ 2 của tuần hiện tại

            var logs = await _context.UserFeatureTimeLogs
                .AsNoTracking()
                .Where(l => l.UserId == userId
                         && l.LoggedDate >= startDate
                         && l.LoggedDate <= startDate.AddDays(6))
                .GroupBy(l => new { l.LoggedDate, l.Feature })
                .Select(g => new
                {
                    g.Key.LoggedDate,
                    g.Key.Feature,
                    TotalSeconds = g.Sum(l => l.DurationSeconds)
                })
                .ToListAsync();

            var lookup = logs.ToDictionary(
                l => (l.LoggedDate.Date, l.Feature),
                l => (int)Math.Round(l.TotalSeconds / 60.0));

            var result = new List<DailyFeatureTimeViewModel>(7);
            for (var i = 0; i < 7; i++)
            {
                var date = startDate.AddDays(i);
                var dayLabel = date.DayOfWeek switch
                {
                    DayOfWeek.Monday    => "T2",
                    DayOfWeek.Tuesday   => "T3",
                    DayOfWeek.Wednesday => "T4",
                    DayOfWeek.Thursday  => "T5",
                    DayOfWeek.Friday    => "T6",
                    DayOfWeek.Saturday  => "T7",
                    _                   => "CN"
                };

                int Get(string feature) => lookup.TryGetValue((date.Date, feature), out var v) ? v : 0;

                result.Add(new DailyFeatureTimeViewModel
                {
                    DayLabel        = dayLabel,
                    FullDate        = date.ToString("dd/MM"),
                    IsToday         = date == today,
                    FlashcardMinutes = Get("Flashcard"),
                    QuizMinutes      = Get("Quiz"),
                    SpeakingMinutes  = Get("Speaking"),
                    ReadingMinutes   = Get("Reading"),
                    ListeningMinutes = Get("Listening"),
                    WritingMinutes   = Get("Writing"),
                    GrammarMinutes   = Get("Grammar"),
                });
            }

            return result;
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckAnswer(int selectedCardId, string challengeToken)
        {
            using var operation = _clock.BeginOperation();
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
                return _context.Database.IsRelational()
                    ? await TCTEnglish.Services.Accounts.UserMutationScope.ExecuteAsync<IActionResult>(
                        _context, userId, () => CompleteDailyChallengeAsync(userId, correctCardId),
                        result => result is JsonResult, HttpContext.RequestAborted)
                    : await CompleteDailyChallengeAsync(userId, correctCardId);
            }

            return Json(new { correct = false, correctCardId, completedToday = false });
        }

        private async Task<IActionResult> CompleteDailyChallengeAsync(int userId, int correctCardId)
        {
                if (await IsDailyChallengeCompletedTodayAsync(userId))
                {
                    return Json(new { correct = true, correctCardId, completedToday = true });
                }

                var activityUpdate = new GoalsActivityUpdate
                {
                    CardsReviewed = 1,
                    QuizzesCompleted = 1,
                    XpEarned = DailyChallengeXp
                };

                var activityResult = await _goalsService.RecordLearningActivityAsync(userId, activityUpdate);
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

                await MarkDailyChallengeCompletedAsync(userId);
                _logger.LogInformation("Correct answer recorded for user {userId}, card {cardId}", userId, correctCardId);
                return Json(new { correct = true, correctCardId, completedToday = true });
        }

        private async Task<DailyChallengeViewModel> GetDailyChallengeForUserAsync(int userId)
        {
            if (await IsDailyChallengeCompletedTodayAsync(userId))
            {
                return new DailyChallengeViewModel { IsCompletedToday = true };
            }

            return await GetRandomChallengeAsync(userId);
        }

        private async Task<bool> IsDailyChallengeCompletedTodayAsync(int userId)
        {
            var today = _clock.BusinessDate;
            return await _context.UserDailyActivities
                .AsNoTracking()
                .AnyAsync(a => a.UserId == userId
                    && a.ActivityDate == today
                    && a.DailyChallengeCompleted);
        }

        private async Task MarkDailyChallengeCompletedAsync(int userId)
        {
            var today = _clock.BusinessDate;
            var activity = await _context.UserDailyActivities
                .SingleOrDefaultAsync(a => a.UserId == userId && a.ActivityDate == today);
            if (activity == null)
            {
                activity = new UserDailyActivity { UserId = userId, ActivityDate = today };
                _context.UserDailyActivities.Add(activity);
            }
            activity.DailyChallengeCompleted = true;
            await _context.SaveChangesAsync();
        }

        private async Task<DailyChallengeViewModel> GetRandomChallengeAsync(int userId)
        {
            var systemCardsQuery = _context.Cards
                .AsNoTracking()
                .Where(c => c.Set.Owner.Role == "System");

            var totalCards = await systemCardsQuery.CountAsync();

            if (totalCards == 0) return new DailyChallengeViewModel();

            var randomIndex = Random.Shared.Next(totalCards);
            var randomCard = await systemCardsQuery
                .Skip(randomIndex)
                .FirstOrDefaultAsync();

            if (randomCard == null) return new DailyChallengeViewModel();

            var wrongAnswers = await GetSystemWrongAnswersAsync(randomCard.CardId);

            wrongAnswers.Add(new AnswerOption
            {
                CardId = randomCard.CardId,
                Definition = randomCard.Definition
            });

            return new DailyChallengeViewModel
            {
                CardId = randomCard.CardId,
                Term = randomCard.Term,
                ChallengeToken = CreateDailyChallengeToken(userId, randomCard.CardId),
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
            var ownFolders = await _context.Folders
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => new TodayFolderViewModel
                {
                    FolderId = f.FolderId,
                    FolderName = f.FolderName,
                    SetCount = f.Sets.Count,
                    CreatorName = f.User.FullName ?? "Unknown",
                    IsOwnedByCurrentUser = true
                })
                .ToListAsync();

            if (ownFolders.Count > 0)
            {
                var ownFolderIds = ownFolders.Select(f => f.FolderId).ToList();
                var lastReviewedByFolder = await _context.LearningProgresses
                    .AsNoTracking()
                    .Where(lp => lp.UserId == userId
                        && lp.LastReviewedDate != null
                        && lp.Card.Set.FolderId != null
                        && ownFolderIds.Contains(lp.Card.Set.FolderId.Value))
                    .GroupBy(lp => lp.Card.Set.FolderId!.Value)
                    .Select(g => new
                    {
                        FolderId = g.Key,
                        LastReviewed = g.Max(lp => lp.LastReviewedDate)
                    })
                    .ToListAsync();

                var lastReviewedMap = lastReviewedByFolder
                    .ToDictionary(x => x.FolderId, x => x.LastReviewed ?? DateTime.MinValue);

                return ownFolders
                    .OrderByDescending(f => lastReviewedMap.TryGetValue(f.FolderId, out var last)
                        ? last
                        : DateTime.MinValue)
                    .ThenByDescending(f => f.FolderId)
                    .Take(3)
                    .ToList();
            }

            var eligibleFolders = _context.Folders
                .AsNoTracking()
                .Where(f => f.UserId != userId);

            if (_context.Database.IsMySql())
            {
                return await eligibleFolders
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .Select(f => new TodayFolderViewModel
                    {
                        FolderId = f.FolderId,
                        FolderName = f.FolderName,
                        SetCount = f.Sets.Count,
                        CreatorName = f.User.FullName ?? "Unknown",
                        IsOwnedByCurrentUser = false
                    })
                    .ToListAsync();
            }

            // SQLite test provider cannot translate Guid.NewGuid() inside OrderBy.
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
                    CreatorName = f.User.FullName ?? "Unknown",
                    IsOwnedByCurrentUser = false
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
            var providerName = _context.Database.ProviderName ?? string.Empty;
            if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                return await eligibleCards
                    .OrderBy(c => Guid.NewGuid())
                    .Take(3)
                    .Select(c => new AnswerOption
                    {
                        CardId = c.CardId,
                        Definition = c.Definition
                    })
                    .ToListAsync();
            }

            if (_context.Database.IsMySql())
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
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var challenge = await GetDailyChallengeForUserAsync(userId);
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
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Contact(string? subject)
        {
            var vm = new ContactFormViewModel
            {
                Subject = ContactFormViewModel.NormalizeSubject(subject),
                ContactNonce = _emailSender.IssueContactNonce(ContactClientPartition())
            };
            // The query parameter is also a ModelState entry; do not let it override the normalized selection.
            ModelState.Remove(nameof(vm.Subject));
            return View(vm);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(32768)]
        [RequestFormLimits(ValueCountLimit = 16, ValueLengthLimit = 24576, KeyLengthLimit = 64, MultipartBodyLengthLimit = 32768)]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Contact(ContactFormViewModel model)
        {
            // MVC keeps the raw attempted values; render exactly the trimmed values validated by the setters.
            ModelState.SetModelValue(nameof(model.Name), model.Name, model.Name);
            ModelState.SetModelValue(nameof(model.Email), model.Email, model.Email);
            ModelState.SetModelValue(nameof(model.Subject), model.Subject, model.Subject);
            ModelState.SetModelValue(nameof(model.Message), model.Message, model.Message);
            if (!ModelState.IsValid)
            {
                model.State = ContactFormState.Invalid;
                RenewContactNonce(model);
                return View(model);
            }
            if (Request.Headers.UserAgent.ToString().Length > 2048
                || Request.Headers.Sum(h => (long)h.Key.Length + h.Value.Sum(v => (long)(v?.Length ?? 0))) > 16384)
            {
                ModelState.AddModelError(string.Empty, "Thông tin yêu cầu vượt giới hạn cho phép.");
                model.State = ContactFormState.Invalid;
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return View(model);
            }
            var result = await _emailSender.SubmitContactAsync(model, ContactClientPartition(),
                Request.Headers.UserAgent.ToString(), HttpContext.RequestAborted);
            if (result == ContactSubmissionStatus.Accepted)
            {
                TempData["ContactSuccessMessage"] = "Máy chủ thư đã tiếp nhận yêu cầu của bạn. Cảm ơn bạn đã liên hệ.";
                return RedirectToAction(nameof(Contact), new { subject = model.Subject });
            }
            (model.State, Response.StatusCode) = result switch
            {
                ContactSubmissionStatus.ConfigurationUnavailable => (ContactFormState.Unavailable, StatusCodes.Status503ServiceUnavailable),
                ContactSubmissionStatus.Failed => (ContactFormState.Failed, StatusCodes.Status503ServiceUnavailable),
                ContactSubmissionStatus.Unknown => (ContactFormState.Unknown, StatusCodes.Status202Accepted),
                ContactSubmissionStatus.Throttled => (ContactFormState.Throttled, StatusCodes.Status429TooManyRequests),
                ContactSubmissionStatus.Duplicate => (ContactFormState.Duplicate, StatusCodes.Status409Conflict),
                ContactSubmissionStatus.InvalidOperation => (ContactFormState.Expired, StatusCodes.Status400BadRequest),
                _ => (ContactFormState.Invalid, StatusCodes.Status400BadRequest)
            };
            if (result is ContactSubmissionStatus.ConfigurationUnavailable or ContactSubmissionStatus.Failed or ContactSubmissionStatus.InvalidOperation)
                RenewContactNonce(model);
            return View(model);
        }

        private string ContactClientPartition() => HttpContext.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unavailable";

        private void RenewContactNonce(ContactFormViewModel model)
        {
            model.ContactNonce = _emailSender.IssueContactNonce(ContactClientPartition());
            ModelState.Remove(nameof(model.ContactNonce));
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

                // Header search: menu điều hướng + thư mục + người dùng.
                // Tìm lớp chỉ nằm trên Khám phá lớp / Lớp học của tôi.
                vm.NavMenus = MatchNavMenus(query);

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

        private List<NavMenuSearchResultViewModel> MatchNavMenus(string query)
        {
            var needle = query.Trim();
            if (needle.Length == 0)
            {
                return new List<NavMenuSearchResultViewModel>();
            }

            static bool Matches(string needle, params string[] terms) =>
                terms.Any(term => term.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || needle.Contains(term, StringComparison.OrdinalIgnoreCase));

            var canCreateClass = User.IsInRole(Roles.Teacher) || User.IsInRole(Roles.Admin);
            var myClassesTitle = canCreateClass ? "Lớp tôi tạo" : "Lớp học của tôi";
            var myClassesDescription = canCreateClass
                ? "Lớp bạn đã tạo và lớp đang tham gia"
                : "Danh sách lớp bạn đang tham gia";

            var catalog = new List<(string Title, string Description, string Url, string Icon, string[] Terms)>
            {
                ("Trang chủ", "Bảng điều khiển học tập", "/Home/Index", "fas fa-home",
                    new[] { "trang chủ", "home", "dashboard" }),
                ("Từ vựng", "Khóa học và bài từ vựng", "/Vocabulary/Index", "fas fa-language",
                    new[] { "từ vựng", "vocabulary", "vocab", "flashcard" }),
                ("Nghe", "Bài nghe và luyện nghe", "/Home/Listening", "fas fa-headphones",
                    new[] { "nghe", "listening", "audio" }),
                ("Đọc", "Bài đọc hiểu", "/Home/Reading", "fas fa-book-open",
                    new[] { "đọc", "reading", "read" }),
                ("Viết", "Luyện viết tiếng Anh", "/Home/Writing", "fas fa-pen-nib",
                    new[] { "viết", "writing", "write" }),
                ("Nói", "Luyện nói và phát âm", "/Speaking/Index", "fas fa-microphone",
                    new[] { "nói", "speaking", "speak", "phát âm" }),
                ("Mục tiêu", "Mục tiêu học tập hàng ngày", "/Goals/Index", "fas fa-bullseye",
                    new[] { "mục tiêu", "goals", "goal" }),
                ("Thông báo", "Hộp thông báo của bạn", "/Notification/Index", "fas fa-bell",
                    new[] { "thông báo", "notifications", "notification" }),
                ("Bộ thẻ của tôi", "Thư viện bộ thẻ flashcard", "/Sets", "fas fa-clone",
                    new[] { "bộ thẻ", "sets", "set", "thẻ" }),
                ("Thư mục", "Quản lý thư mục học tập", "/Sets", "fas fa-folder",
                    new[] { "thư mục", "folder", "folders" }),
                ("Khám phá lớp", "Tìm lớp Public để tham gia", "/Home/BrowseClasses", "fas fa-compass",
                    new[] { "khám phá lớp", "lớp public", "browse class", "tìm lớp" }),
                (myClassesTitle, myClassesDescription, "/Home/Class", "fas fa-chalkboard-teacher",
                    new[] { "lớp học", "lớp", "class", "classroom", "virtual classroom", "lớp tôi tạo" }),
                ("Liên hệ", "Gửi liên hệ hỗ trợ", "/Home/Contact", "fas fa-envelope",
                    new[] { "liên hệ", "contact", "hỗ trợ", "support" }),
                ("Gói Premium", "Xem và nâng cấp Premium", "/Premium", "fas fa-crown",
                    new[] { "premium", "nâng cấp", "pricing", "gói" }),
            };

            if (canCreateClass)
            {
                catalog.Add(("Tạo lớp học", "Tạo lớp học ảo mới", "/Home/CreateClass", "fas fa-plus",
                    new[] { "tạo lớp", "create class", "new class" }));
            }

            return catalog
                .Where(item => Matches(needle, item.Terms) || Matches(needle, item.Title, item.Description))
                .Select(item => new NavMenuSearchResultViewModel
                {
                    Title = item.Title,
                    Description = item.Description,
                    Url = item.Url,
                    IconClass = item.Icon
                })
                .ToList();
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
