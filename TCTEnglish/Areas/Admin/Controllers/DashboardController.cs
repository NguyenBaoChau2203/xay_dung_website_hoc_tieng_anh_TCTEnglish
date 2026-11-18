using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class DashboardController : Controller
    {
        private readonly DbflashcardContext _context;

        public DashboardController(DbflashcardContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int range = 7)
        {
            var rangeDays = range == 30 ? 30 : 7;
            var today = DateTime.UtcNow.Date;
            var periodStart = today.AddDays(-(rangeDays - 1));
            var previousStart = periodStart.AddDays(-rangeDays);
            var previousEnd = periodStart.AddDays(-1);
            var sevenDaysAgo = today.AddDays(-6);

            var userStats = await _context.Users.AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    NewInPeriod = g.Count(u => u.CreatedAt >= periodStart),
                    NewLast7Days = g.Count(u => u.CreatedAt >= sevenDaysAgo),
                    Blocked = g.Count(u => u.Status == UserStatus.Blocked)
                })
                .FirstOrDefaultAsync();

            var totalUsers = userStats?.Total ?? 0;
            var newUsersInPeriod = userStats?.NewInPeriod ?? 0;
            var newUsersLast7Days = userStats?.NewLast7Days ?? 0;
            var blockedCount = userStats?.Blocked ?? 0;

            var classCount = await _context.Classes.AsNoTracking().CountAsync();
            var setCount = await _context.Sets.AsNoTracking().CountAsync();
            var speakingCount = await _context.SpeakingVideos.AsNoTracking().CountAsync();
            var listeningCount = await _context.ListeningLessons.AsNoTracking().CountAsync();
            var writingCount = await _context.WritingExercises.AsNoTracking().CountAsync();
            var readingCount = await _context.ReadingPassages.AsNoTracking().CountAsync();
            var catalogTotal = setCount + speakingCount + listeningCount + writingCount + readingCount + classCount;

            var newClassesLast7Days = await _context.Classes.AsNoTracking()
                .CountAsync(c => c.CreatedAt >= sevenDaysAgo);
            var newSetsLast7Days = await _context.Sets.AsNoTracking()
                .CountAsync(s => s.CreatedAt >= sevenDaysAgo);

            var periodActivities = await _context.UserDailyActivities.AsNoTracking()
                .Where(a => a.ActivityDate >= periodStart && a.ActivityDate <= today)
                .Select(a => new
                {
                    a.UserId,
                    a.ActivityDate,
                    a.XpEarned,
                    a.VocabularyCompletedCount,
                    a.SpeakingCompletedCount,
                    a.ListeningCompletedCount,
                    a.ReadingCompletedCount,
                    a.WritingCompletedCount,
                    UserName = a.User.FullName ?? a.User.Email ?? ("#" + a.UserId)
                })
                .ToListAsync();

            var previousActivities = await _context.UserDailyActivities.AsNoTracking()
                .Where(a => a.ActivityDate >= previousStart && a.ActivityDate <= previousEnd)
                .Select(a => new
                {
                    a.UserId,
                    Completions = a.VocabularyCompletedCount
                        + a.SpeakingCompletedCount
                        + a.ListeningCompletedCount
                        + a.ReadingCompletedCount
                        + a.WritingCompletedCount
                })
                .ToListAsync();

            static int DayCompletions(int vocab, int speaking, int listening, int reading, int writing) =>
                vocab + speaking + listening + reading + writing;

            var activeLearners = periodActivities.Select(a => a.UserId).Distinct().Count();
            var previousActiveLearners = previousActivities.Select(a => a.UserId).Distinct().Count();

            var completionsInPeriod = periodActivities.Sum(a =>
                DayCompletions(
                    a.VocabularyCompletedCount,
                    a.SpeakingCompletedCount,
                    a.ListeningCompletedCount,
                    a.ReadingCompletedCount,
                    a.WritingCompletedCount));
            var completionsPrevious = previousActivities.Sum(a => a.Completions);

            var dailyMap = periodActivities
                .GroupBy(a => a.ActivityDate.Date)
                .ToDictionary(
                    g => g.Key,
                    g => new DashboardDailyPointViewModel
                    {
                        Date = g.Key,
                        Completions = g.Sum(x => DayCompletions(
                            x.VocabularyCompletedCount,
                            x.SpeakingCompletedCount,
                            x.ListeningCompletedCount,
                            x.ReadingCompletedCount,
                            x.WritingCompletedCount)),
                        XpEarned = g.Sum(x => x.XpEarned),
                        ActiveLearners = g.Select(x => x.UserId).Distinct().Count()
                    });

            var dailyActivity = new List<DashboardDailyPointViewModel>(rangeDays);
            for (var day = periodStart; day <= today; day = day.AddDays(1))
            {
                if (dailyMap.TryGetValue(day, out var point))
                {
                    dailyActivity.Add(point);
                }
                else
                {
                    dailyActivity.Add(new DashboardDailyPointViewModel { Date = day });
                }
            }

            var vocabDone = periodActivities.Sum(a => a.VocabularyCompletedCount);
            var speakingDone = periodActivities.Sum(a => a.SpeakingCompletedCount);
            var listeningDone = periodActivities.Sum(a => a.ListeningCompletedCount);
            var readingDone = periodActivities.Sum(a => a.ReadingCompletedCount);
            var writingDone = periodActivities.Sum(a => a.WritingCompletedCount);
            var skillMax = Math.Max(1, new[] { vocabDone, speakingDone, listeningDone, readingDone, writingDone }.Max());

            var skillBars = new List<DashboardSkillBarViewModel>
            {
                Skill("vocabulary", "Từ vựng", vocabDone, skillMax, "SetManagement", "bi-journals"),
                Skill("speaking", "Nói", speakingDone, skillMax, "SpeakingVideoManagement", "bi-mic-fill"),
                Skill("listening", "Nghe", listeningDone, skillMax, "ListeningManagement", "bi-headphones"),
                Skill("reading", "Đọc", readingDone, skillMax, "ReadingManagement", "bi-book-fill"),
                Skill("writing", "Viết", writingDone, skillMax, "WritingExerciseManagement", "bi-pencil-square")
            };

            var tools = new List<DashboardToolCardViewModel>
            {
                Tool("Vocabulary Sets", setCount, "SetManagement", "bi-journals", "primary"),
                Tool("Speaking Videos", speakingCount, "SpeakingVideoManagement", "bi-mic-fill", "info"),
                Tool("Listening Lessons", listeningCount, "ListeningManagement", "bi-headphones", "success"),
                Tool("Writing Exercises", writingCount, "WritingExerciseManagement", "bi-pencil-square", "warning"),
                Tool("Reading Passages", readingCount, "ReadingManagement", "bi-book-fill", "primary"),
                Tool("Classes", classCount, "ClassManagement", "bi-collection-fill", "success")
            };

            var attentionItems = new List<DashboardAttentionItemViewModel>();
            if (blockedCount > 0)
            {
                attentionItems.Add(new DashboardAttentionItemViewModel
                {
                    Title = $"{blockedCount} tài khoản bị khóa",
                    Detail = "Kiểm tra và xử lý trong quản lý người dùng.",
                    Severity = "danger",
                    Href = Url.Action("Index", "UserManagement", new { area = "Admin" })
                });
            }

            if (newUsersLast7Days > 0)
            {
                attentionItems.Add(new DashboardAttentionItemViewModel
                {
                    Title = $"{newUsersLast7Days} người dùng mới (7 ngày)",
                    Detail = "Duyệt vai trò và trạng thái nếu cần.",
                    Severity = "info",
                    Href = Url.Action("Index", "UserManagement", new { area = "Admin" })
                });
            }

            if (activeLearners == 0)
            {
                attentionItems.Add(new DashboardAttentionItemViewModel
                {
                    Title = "Chưa có hoạt động học trong kỳ",
                    Detail = "Bổ sung nội dung hoặc kiểm tra học viên.",
                    Severity = "warning",
                    Href = Url.Action("Index", "SetManagement", new { area = "Admin" })
                });
            }

            var activityFeed = periodActivities
                .Select(a => new
                {
                    a.ActivityDate,
                    a.UserName,
                    Skills = new (string Label, int Count)[]
                    {
                        ("Từ vựng", a.VocabularyCompletedCount),
                        ("Nói", a.SpeakingCompletedCount),
                        ("Nghe", a.ListeningCompletedCount),
                        ("Đọc", a.ReadingCompletedCount),
                        ("Viết", a.WritingCompletedCount)
                    }
                })
                .SelectMany(row => row.Skills
                    .Where(s => s.Count > 0)
                    .Select(s => new DashboardActivityItemViewModel
                    {
                        OccurredOn = row.ActivityDate,
                        SkillLabel = s.Label,
                        Message = $"{row.UserName} hoàn thành {s.Count} mục {s.Label.ToLowerInvariant()}"
                    }))
                .OrderByDescending(i => i.OccurredOn)
                .ThenBy(i => i.SkillLabel)
                .Take(10)
                .ToList();

            var recentUsers = await _context.Users.AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .Select(u => new RecentUserViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? "N/A",
                    Email = u.Email,
                    Role = u.Role ?? Roles.Standard,
                    Status = u.Status,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            foreach (var user in recentUsers)
            {
                user.Role = Roles.Normalize(user.Role);
            }

            var rangeLabel = rangeDays == 30 ? "so với 30 ngày trước" : "so với 7 ngày trước";

            var viewModel = new AdminDashboardViewModel
            {
                SelectedRangeDays = rangeDays,
                TotalUsers = totalUsers,
                TotalClasses = classCount,
                TotalSets = setCount,
                NewUsersLast7Days = newUsersLast7Days,
                NewClassesLast7Days = newClassesLast7Days,
                NewSetsLast7Days = newSetsLast7Days,
                TotalUsersKpi = new DashboardKpiCardViewModel
                {
                    Label = "Tổng người dùng",
                    Value = totalUsers,
                    Delta = newUsersInPeriod > 0 ? newUsersInPeriod : null,
                    DeltaLabel = newUsersInPeriod > 0 ? $"+{newUsersInPeriod} trong kỳ" : null,
                    IconClass = "bi-people-fill",
                    Tone = "primary"
                },
                ActiveLearnersKpi = new DashboardKpiCardViewModel
                {
                    Label = "Người học có hoạt động",
                    Value = activeLearners,
                    Delta = activeLearners - previousActiveLearners != 0
                        ? activeLearners - previousActiveLearners
                        : null,
                    DeltaLabel = activeLearners - previousActiveLearners != 0
                        ? FormatDelta(activeLearners - previousActiveLearners, rangeLabel)
                        : null,
                    IconClass = "bi-person-check-fill",
                    Tone = "success"
                },
                CompletionsKpi = new DashboardKpiCardViewModel
                {
                    Label = "Hoàn thành kỹ năng",
                    Value = completionsInPeriod,
                    Delta = completionsInPeriod - completionsPrevious != 0
                        ? completionsInPeriod - completionsPrevious
                        : null,
                    DeltaLabel = completionsInPeriod - completionsPrevious != 0
                        ? FormatDelta(completionsInPeriod - completionsPrevious, rangeLabel)
                        : null,
                    IconClass = "bi-trophy-fill",
                    Tone = "warning"
                },
                CatalogKpi = new DashboardKpiCardViewModel
                {
                    Label = "Catalog nội dung",
                    Value = catalogTotal,
                    Delta = null,
                    DeltaLabel = null,
                    IconClass = "bi-grid-1x2-fill",
                    Tone = "info"
                },
                DailyActivity = dailyActivity,
                SkillBars = skillBars,
                Tools = tools,
                AttentionItems = attentionItems,
                ActivityFeed = activityFeed,
                RecentUsers = recentUsers
            };

            return View(viewModel);
        }

        private static DashboardSkillBarViewModel Skill(
            string key,
            string label,
            int count,
            int max,
            string controller,
            string icon)
        {
            return new DashboardSkillBarViewModel
            {
                Key = key,
                Label = label,
                Count = count,
                Percent = (int)Math.Round(100.0 * count / max),
                Controller = controller,
                IconClass = icon
            };
        }

        private static DashboardToolCardViewModel Tool(
            string title,
            int count,
            string controller,
            string icon,
            string tone)
        {
            return new DashboardToolCardViewModel
            {
                Title = title,
                Count = count,
                Controller = controller,
                IconClass = icon,
                Tone = tone
            };
        }

        private static string FormatDelta(int delta, string rangeLabel)
        {
            var sign = delta > 0 ? "+" : string.Empty;
            return $"{sign}{delta} {rangeLabel}";
        }
    }
}
