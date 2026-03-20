using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class HomeController : BaseController
    {
        private readonly DbflashcardContext _context;
        private readonly IAppEmailSender _emailSender;
        private readonly IStreakService _streakService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            DbflashcardContext context,
            IAppEmailSender emailSender,
            IStreakService streakService,
            ILogger<HomeController> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _streakService = streakService;
            _logger = logger;
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

            return new DashboardViewModel
            {
                FullName = user.FullName,
                Streak = user.Streak ?? 0,
                Goal = user.Goal ?? 0,
                FolderCount = user.Folders.Count,
                SetCount = user.Sets.Count,
                CardCount = cardCount,
                DailyChallenge = await GetRandomChallengeAsync(),
                TodayFolders = todayFolders
            };
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckAnswer(int selectedCardId, int correctCardId)
        {
            var isCorrect = selectedCardId == correctCardId;

            if (isCorrect)
            {
                if (!TryGetCurrentUserId(out var userId))
                {
                    _logger.LogWarning("Unauthorized check answer request for card {cardId}", correctCardId);
                    return Unauthorized();
                }

                await _streakService.UpdateStreakAsync(userId);
                _logger.LogInformation("Correct answer recorded for user {userId}, card {cardId}", userId, correctCardId);
            }

            return Json(new { correct = isCorrect });
        }

        private async Task<DailyChallengeViewModel> GetRandomChallengeAsync()
        {
            var totalCards = await _context.Cards.CountAsync();
            if (totalCards == 0)
            {
                return new DailyChallengeViewModel();
            }

            var randomIndex = Random.Shared.Next(totalCards);
            var randomCard = await _context.Cards
                .AsNoTracking()
                .Skip(randomIndex)
                .FirstAsync();

            var wrongAnswers = await GetWrongAnswersAsync(randomCard.CardId);

            wrongAnswers.Add(new AnswerOption
            {
                CardId = randomCard.CardId,
                Definition = randomCard.Definition
            });

            return new DailyChallengeViewModel
            {
                CardId = randomCard.CardId,
                Term = randomCard.Term,
                Options = wrongAnswers.OrderBy(_ => Random.Shared.Next()).ToList()
            };
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

        private async Task<List<AnswerOption>> GetWrongAnswersAsync(int excludedCardId)
        {
            var eligibleCards = _context.Cards
                .AsNoTracking()
                .Where(c => c.CardId != excludedCardId);

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

            // FIX: SQLite test provider cannot translate Guid.NewGuid() inside OrderBy.
            var cardIds = await TakeRandomIdsAsync(
                eligibleCards
                    .OrderBy(c => c.CardId)
                    .Select(c => c.CardId),
                3);

            if (cardIds.Count == 0)
            {
                return new List<AnswerOption>();
            }

            var answers = await eligibleCards
                .Where(c => cardIds.Contains(c.CardId))
                .Select(c => new AnswerOption
                {
                    CardId = c.CardId,
                    Definition = c.Definition
                })
                .ToListAsync();

            var answersById = answers.ToDictionary(a => a.CardId);
            return cardIds
                .Where(answersById.ContainsKey)
                .Select(id => answersById[id])
                .ToList();
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
