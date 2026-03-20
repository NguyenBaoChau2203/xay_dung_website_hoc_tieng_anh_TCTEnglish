using Microsoft.EntityFrameworkCore;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services
{
    public partial class StudyService
    {
        private const string SystemVocabularyEmail = "system@tct.local";

        public async Task<VocabularyIndexViewModel> GetVocabularyIndexViewModelAsync(int currentUserId)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return new VocabularyIndexViewModel();
            }

            var folders = await GetVocabularyFolderSectionsAsync(currentUserId, systemUserId.Value);
            var allSets = folders.SelectMany(folder => folder.Sets).ToList();

            var stats = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == currentUserId)
                .Select(user => new VocabularyDashboardStatsViewModel
                {
                    Streak = user.Streak ?? 0,
                    LongestStreak = user.LongestStreak ?? 0
                })
                .FirstOrDefaultAsync() ?? new VocabularyDashboardStatsViewModel();

            stats.TotalSets = allSets.Count;
            stats.TotalCards = allSets.Sum(set => set.CardCount);
            stats.MasteredCards = allSets.Sum(set => set.MasteredCardCount);
            stats.DueToday = allSets.Sum(set => set.DueTodayCount);

            var lastStudiedSetId = await GetLastStudiedSetIdAsync(currentUserId, systemUserId.Value);
            var continueSet = lastStudiedSetId.HasValue
                ? allSets.FirstOrDefault(set => set.SetId == lastStudiedSetId.Value)
                : allSets.FirstOrDefault();

            if (continueSet == null)
            {
                continueSet = allSets.FirstOrDefault();
            }

            return new VocabularyIndexViewModel
            {
                Stats = stats,
                ContinueLearning = BuildContinueLearningViewModel(
                    continueSet,
                    lastStudiedSetId.HasValue && continueSet?.SetId == lastStudiedSetId.Value),
                AllTags = allSets
                    .SelectMany(set => set.Tags)
                    .Distinct()
                    .OrderBy(tag => tag)
                    .ToList(),
                Folders = folders
            };
        }

        public async Task<VocabularySetDetailViewModel?> GetVocabularySetDetailViewModelAsync(int setId, int currentUserId)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return null;
            }

            var source = await GetVocabularySetSourceAsync(setId, currentUserId, systemUserId.Value);
            if (source == null)
            {
                return null;
            }

            var relatedSets = await GetRelatedSetsAsync(source.Set.FolderId, source.Set.SetId);
            return BuildDetailViewModel(source, relatedSets);
        }

        public async Task<VocabularyTopicsViewModel?> GetVocabularyTopicsViewModelAsync(int setId, int currentUserId)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return null;
            }

            var source = await GetVocabularySetSourceAsync(setId, currentUserId, systemUserId.Value);
            return source == null ? null : BuildTopicsViewModel(source);
        }

        public async Task<VocabularyTopicDetailViewModel?> GetVocabularyTopicDetailViewModelAsync(
            int setId,
            int currentUserId,
            string? topic)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return null;
            }

            var source = await GetVocabularySetSourceAsync(setId, currentUserId, systemUserId.Value);
            return source == null ? null : BuildTopicDetailViewModel(source, topic);
        }

        public async Task<StudyViewModel?> GetVocabularyStudyViewModelAsync(
            int setId,
            int currentUserId,
            string? topic,
            int index,
            string mode)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return null;
            }

            var source = await GetVocabularySetSourceAsync(setId, currentUserId, systemUserId.Value);
            return source == null ? null : BuildStudyPageViewModel(source, topic, index, mode);
        }

        public async Task<VocabularyFolderDetailViewModel?> GetVocabularyFolderDetailViewModelAsync(int folderId, int currentUserId)
        {
            var systemUserId = await GetSystemUserIdAsync();
            if (!systemUserId.HasValue)
            {
                return null;
            }

            var folder = (await GetVocabularyFolderSectionsAsync(currentUserId, systemUserId.Value, folderId))
                .FirstOrDefault();

            return folder == null ? null : BuildVocabularyFolderDetailViewModel(folder);
        }

        public async Task<bool> TryIncrementVocabularySetViewCountAsync(int setId)
        {
            var trackedSet = await _context.Sets
                .FirstOrDefaultAsync(set => set.SetId == setId && set.Owner != null && set.Owner.Email == SystemVocabularyEmail);

            if (trackedSet == null)
            {
                return false;
            }

            trackedSet.ViewCount++;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<int?> GetSystemUserIdAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Where(user => user.Email == SystemVocabularyEmail)
                .Select(user => (int?)user.UserId)
                .FirstOrDefaultAsync();
        }

        private async Task<List<VocabularyFolderSectionViewModel>> GetVocabularyFolderSectionsAsync(
            int currentUserId,
            int systemUserId,
            int? folderId = null)
        {
            var utcNow = DateTime.UtcNow;
            var folderQuery = _context.Folders
                .AsNoTracking()
                .Where(folder => folder.UserId == systemUserId);

            if (folderId.HasValue)
            {
                folderQuery = folderQuery.Where(folder => folder.FolderId == folderId.Value);
            }

            var folders = await folderQuery
                .OrderBy(folder => folder.FolderId)
                .Select(folder => new VocabularyFolderSectionViewModel
                {
                    FolderId = folder.FolderId,
                    FolderName = folder.FolderName,
                    Sets = folder.Sets
                        .OrderBy(set => set.SetId)
                        .Select(set => new VocabularySetListItemViewModel
                        {
                            SetId = set.SetId,
                            SetName = set.SetName,
                            Description = set.Description,
                            CardCount = set.Cards.Count(),
                            MasteredCardCount = set.Cards.Count(card =>
                                card.LearningProgresses.Any(progress =>
                                    progress.UserId == currentUserId &&
                                    progress.Status == "Mastered")),
                            DueTodayCount = set.Cards.Count(card =>
                                card.LearningProgresses.Any(progress =>
                                    progress.UserId == currentUserId &&
                                    progress.NextReviewDate != null &&
                                    progress.NextReviewDate <= utcNow)),
                            ViewCount = set.ViewCount
                        })
                        .ToList()
                })
                .ToListAsync();

            EnrichVocabularyFolderSections(folders);
            return folders;
        }

        private async Task<int?> GetLastStudiedSetIdAsync(int currentUserId, int systemUserId)
        {
            return await _context.LearningProgresses
                .AsNoTracking()
                .Where(progress =>
                    progress.UserId == currentUserId &&
                    progress.LastReviewedDate != null &&
                    progress.Card.Set.OwnerId == systemUserId)
                .OrderByDescending(progress => progress.LastReviewedDate)
                .Select(progress => (int?)progress.Card.SetId)
                .FirstOrDefaultAsync();
        }

        private async Task<StudyViewModel?> GetVocabularySetSourceAsync(int setId, int currentUserId, int systemUserId)
        {
            var utcNow = DateTime.UtcNow;

            return await _context.Sets
                .AsNoTracking()
                .Where(set => set.SetId == setId && set.OwnerId == systemUserId)
                .Select(set => new StudyViewModel
                {
                    Set = new VocabularySetSummaryViewModel
                    {
                        SetId = set.SetId,
                        SetName = set.SetName,
                        FolderId = set.FolderId,
                        FolderName = set.Folder != null ? set.Folder.FolderName : null,
                        Description = set.Description
                    },
                    Cards = set.Cards
                        .OrderBy(card => card.CardId)
                        .Select(card => new VocabularyCardItemViewModel
                        {
                            CardId = card.CardId,
                            Term = card.Term,
                            Definition = card.Definition,
                            ImageUrl = card.ImageUrl,
                            Phonetic = card.Phonetic,
                            Example = card.Example,
                            ExampleTranslation = card.ExampleTranslation,
                            Topic = card.Topic ?? "Chưa phân loại",
                            LearningStatus = card.LearningProgresses
                                .Where(progress => progress.UserId == currentUserId)
                                .Select(progress => progress.Status)
                                .FirstOrDefault() ?? "New",
                            IsDueForReview = card.LearningProgresses
                                .Any(progress => progress.UserId == currentUserId
                                    && progress.NextReviewDate != null
                                    && progress.NextReviewDate <= utcNow),
                            IsLearned = card.LearningProgresses
                                .Any(progress => progress.UserId == currentUserId
                                    && (progress.Status == "Mastered" || progress.Status == "Learned"))
                        })
                        .ToList(),
                    StudyTotal = set.Cards.Count()
                })
                .FirstOrDefaultAsync();
        }

        private async Task<List<VocabularyRelatedSetViewModel>> GetRelatedSetsAsync(int? folderId, int currentSetId)
        {
            if (!folderId.HasValue)
            {
                return new List<VocabularyRelatedSetViewModel>();
            }

            var relatedSets = await _context.Sets
                .AsNoTracking()
                .Where(set => set.FolderId == folderId.Value && set.SetId != currentSetId)
                .OrderBy(set => set.SetId)
                .Take(3)
                .Select(set => new
                {
                    set.SetId,
                    set.SetName,
                    set.Description,
                    CardCount = set.Cards.Count()
                })
                .ToListAsync();

            return relatedSets
                .Select(set => new VocabularyRelatedSetViewModel
                {
                    SetId = set.SetId,
                    SetName = set.SetName,
                    CardCount = set.CardCount,
                    Tags = ExtractTags(set.Description)
                })
                .ToList();
        }

        private static VocabularyFolderDetailViewModel BuildVocabularyFolderDetailViewModel(
            VocabularyFolderSectionViewModel folder)
        {
            return new VocabularyFolderDetailViewModel
            {
                FolderId = folder.FolderId,
                FolderName = folder.FolderName,
                TotalSets = folder.SetCount,
                TotalCards = folder.Sets.Sum(set => set.CardCount),
                TotalViews = folder.Sets.Sum(set => set.ViewCount),
                Sets = folder.Sets
            };
        }

        private static VocabularyContinueLearningViewModel? BuildContinueLearningViewModel(
            VocabularySetListItemViewModel? set,
            bool hasProgress)
        {
            if (set == null)
            {
                return null;
            }

            return new VocabularyContinueLearningViewModel
            {
                SetId = set.SetId,
                SetName = set.SetName,
                CardCount = set.CardCount,
                RemainingCardCount = set.RemainingCardCount,
                ProgressPercentage = set.ProgressPercentage,
                HasProgress = hasProgress
            };
        }

        private static void EnrichVocabularyFolderSections(IEnumerable<VocabularyFolderSectionViewModel> folders)
        {
            foreach (var folder in folders)
            {
                foreach (var set in folder.Sets)
                {
                    EnrichVocabularySetItem(set);
                }

                folder.SetCount = folder.Sets.Count;
            }
        }

        private static void EnrichVocabularySetItem(VocabularySetListItemViewModel set)
        {
            set.Tags = ExtractTags(set.Description);
            set.DescriptionText = string.IsNullOrWhiteSpace(set.Description)
                ? "Không có mô tả"
                : set.Description.Replace("#", string.Empty).Trim();
            set.RemainingCardCount = Math.Max(0, set.CardCount - set.MasteredCardCount);
            set.ProgressPercentage = set.CardCount > 0
                ? (set.MasteredCardCount * 100) / set.CardCount
                : 0;
            set.EstimatedMinutes = Math.Max(1, set.CardCount * 2);
        }

        private static StudyViewModel BuildStudyPageViewModel(StudyViewModel source, string? topic, int index, string mode)
        {
            var normalizedTopic = NormalizeTopic(topic);
            var isReview = string.Equals(mode, "review", StringComparison.OrdinalIgnoreCase);

            var filteredCards = source.Cards.ToList();
            if (!string.IsNullOrWhiteSpace(topic))
            {
                filteredCards = filteredCards
                    .Where(card => card.Topic == normalizedTopic)
                    .ToList();
            }

            if (isReview)
            {
                filteredCards = filteredCards
                    .Where(card => card.IsDueForReview)
                    .ToList();
            }

            var safeIndex = filteredCards.Count == 0
                ? 1
                : Math.Clamp(index, 1, filteredCards.Count);

            return new StudyViewModel
            {
                Set = new VocabularySetSummaryViewModel
                {
                    SetId = source.Set.SetId,
                    SetName = string.IsNullOrWhiteSpace(topic)
                        ? source.Set.SetName
                        : $"{source.Set.SetName} - {normalizedTopic}",
                    FolderId = source.Set.FolderId,
                    FolderName = source.Set.FolderName,
                    Description = source.Set.Description
                },
                Cards = filteredCards,
                MasteredCardIds = filteredCards
                    .Where(card => card.LearningStatus == "Mastered")
                    .Select(card => card.CardId)
                    .ToList(),
                LearningCardIds = filteredCards
                    .Where(card => card.LearningStatus == "Learning" || card.LearningStatus == "Reviewing")
                    .Select(card => card.CardId)
                    .ToList(),
                CurrentIndex = safeIndex,
                TopicName = string.IsNullOrWhiteSpace(topic) ? null : normalizedTopic,
                IsReview = isReview,
                StudyMode = mode,
                StudyTotal = filteredCards.Count
            };
        }

        private static VocabularySetDetailViewModel BuildDetailViewModel(
            StudyViewModel source,
            List<VocabularyRelatedSetViewModel> relatedSets)
        {
            return new VocabularySetDetailViewModel
            {
                Set = source.Set,
                Stats = BuildSetStats(source.Cards),
                Tags = ExtractTags(source.Set.Description),
                Topics = BuildTopicSummaries(source.Cards),
                RelatedSets = relatedSets
            };
        }

        private static VocabularyTopicsViewModel BuildTopicsViewModel(StudyViewModel source)
        {
            return new VocabularyTopicsViewModel
            {
                Set = source.Set,
                Stats = BuildSetStats(source.Cards),
                Topics = BuildTopicSummaries(source.Cards)
            };
        }

        private static VocabularyTopicDetailViewModel BuildTopicDetailViewModel(StudyViewModel source, string? topic)
        {
            var normalizedTopic = NormalizeTopic(topic);
            var topicCards = source.Cards
                .Where(card => card.Topic == normalizedTopic)
                .ToList();

            return new VocabularyTopicDetailViewModel
            {
                Set = source.Set,
                Topic = BuildTopicSummary(normalizedTopic, topicCards),
                Stats = BuildSetStats(topicCards),
                Cards = topicCards
                    .Select(card => new VocabularyTopicCardViewModel
                    {
                        CardId = card.CardId,
                        Term = card.Term,
                        Definition = card.Definition,
                        Phonetic = card.Phonetic,
                        Example = card.Example,
                        ExampleTranslation = card.ExampleTranslation,
                        ImageUrl = card.ImageUrl,
                        StatusClass = GetStatusClass(card.LearningStatus),
                        StatusText = GetStatusText(card.LearningStatus)
                    })
                    .ToList()
            };
        }

        private static VocabularySetStatsViewModel BuildSetStats(IReadOnlyCollection<VocabularyCardItemViewModel> cards)
        {
            var totalCards = cards.Count;
            var learnedCards = cards.Count(card => card.IsLearned);

            return new VocabularySetStatsViewModel
            {
                TotalCards = totalCards,
                LearnedCards = learnedCards,
                RemainingCards = totalCards - learnedCards,
                Accuracy = totalCards > 0 ? (learnedCards * 100) / totalCards : 0,
                NewCards = cards.Count(card => card.LearningStatus == "New"),
                ReviewingCards = cards.Count(card =>
                    card.LearningStatus == "Reviewing" || card.LearningStatus == "Learning"),
                MasteredCards = cards.Count(card => card.LearningStatus == "Mastered"),
                DueToday = cards.Count(card => card.IsDueForReview)
            };
        }

        private static List<VocabularyTopicSummaryViewModel> BuildTopicSummaries(IEnumerable<VocabularyCardItemViewModel> cards)
        {
            return cards
                .GroupBy(card => NormalizeTopic(card.Topic))
                .Select(group => BuildTopicSummary(group.Key, group.ToList()))
                .OrderBy(topic => topic.TopicName)
                .ToList();
        }

        private static VocabularyTopicSummaryViewModel BuildTopicSummary(
            string topicName,
            IReadOnlyCollection<VocabularyCardItemViewModel> cards)
        {
            var totalCards = cards.Count;
            var learnedCards = cards.Count(card => card.IsLearned);

            return new VocabularyTopicSummaryViewModel
            {
                TopicName = topicName,
                TotalCards = totalCards,
                LearnedCards = learnedCards,
                Progress = totalCards > 0 ? (learnedCards * 100) / totalCards : 0
            };
        }

        private static List<string> ExtractTags(string? description)
        {
            return string.IsNullOrWhiteSpace(description)
                ? new List<string>()
                : description
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(tag => tag.StartsWith("#"))
                    .ToList();
        }

        private static string NormalizeTopic(string? topic)
        {
            return string.IsNullOrWhiteSpace(topic) ? "Chưa phân loại" : topic;
        }

        private static string GetStatusClass(string learningStatus)
        {
            return learningStatus switch
            {
                "Mastered" => "mastered",
                "Learning" => "reviewing",
                "Reviewing" => "reviewing",
                _ => "new"
            };
        }

        private static string GetStatusText(string learningStatus)
        {
            return learningStatus switch
            {
                "Mastered" => "Thành thạo",
                "Learning" => "Đang ôn",
                "Reviewing" => "Đang ôn",
                _ => "Mới"
            };
        }
    }
}
