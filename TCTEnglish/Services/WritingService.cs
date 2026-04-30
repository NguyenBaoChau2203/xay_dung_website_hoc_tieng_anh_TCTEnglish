using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services
{
    public partial class WritingService : IWritingService
    {
        private static readonly HashSet<string> CommonEnglishStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "are", "as", "at", "be", "been", "being", "but", "by",
            "for", "from", "had", "has", "have", "he", "her", "hers", "him", "his",
            "i", "if", "in", "is", "it", "its", "me", "my", "of", "on", "or", "our",
            "ours", "she", "that", "the", "their", "theirs", "them", "there", "they",
            "this", "to", "us", "was", "we", "were", "will", "with", "you", "your", "yours"
        };
        private const string WritingStatusAll = "all";
        private const string WritingStatusNotStarted = "not-started";
        private const string WritingStatusInProgress = "in-progress";
        private const string WritingStatusCompleted = "completed";
        private const int SummaryTitleMaxLength = 200;
        private const int SummaryTextMaxLength = 500;
        private const int ReviewTextMaxLength = 2000;
        private const int FeedbackFieldMaxLength = 500;
        private const int SuggestedRewriteMaxLength = 1000;
        private readonly DbflashcardContext _context;
        private readonly IWritingAiEvaluationService _writingAiEvaluationService;
        private readonly IGoalsService _goalsService;

        public WritingService(
            DbflashcardContext context,
            IWritingAiEvaluationService writingAiEvaluationService,
            IGoalsService goalsService,
            IAiProviderClient aiProviderClient,
            IAiTokenCounter aiTokenCounter,
            IOptions<AiOptions> aiOptions,
            ILogger<WritingService> logger)
        {
            _context = context;
            _writingAiEvaluationService = writingAiEvaluationService;
            _goalsService = goalsService;
            _aiProviderClient = aiProviderClient;
            _aiTokenCounter = aiTokenCounter;
            _aiOptions = aiOptions.Value;
            _logger = logger;
        }

        public Task<WritingIndexViewModel> GetWritingIndexViewModelAsync(string? selectedLevel)
        {
            return Task.FromResult(BuildWritingIndexViewModel(selectedLevel));
        }

        public async Task<WritingExerciseDataViewModel> GetWritingExerciseDataAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            int? userId = null,
            string? status = null)
        {
            var catalog = await GetWritingExerciseCatalogAsync(selectedLevel, contentType, userId);
            return BuildWritingExerciseDataViewModel(catalog, topic, status);
        }

        public async Task<WritingExerciseListViewModel> GetWritingExerciseListViewModelAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            int page,
            int? userId = null,
            string? status = null)
        {
            var exerciseData = await GetWritingExerciseDataAsync(selectedLevel, contentType, topic, userId, status);
            return BuildWritingExerciseListViewModel(exerciseData, page);
        }

        public async Task<WritingPracticeDataViewModel?> GetWritingPracticeDataAsync(int exerciseId, int userId)
        {
            var exercise = await ResolveWritingExerciseAccessAsync(exerciseId, userId);

            if (exercise == null)
            {
                return null;
            }

            var sentences = await _context.WritingExerciseSentences
                .AsNoTracking()
                .Where(sentence => sentence.WritingExerciseId == exerciseId)
                .OrderBy(sentence => sentence.SortOrder)
                .ToListAsync();

            if (sentences.Count == 0)
            {
                return null;
            }

            var attempts = await LoadWritingAttemptRowsAsync(userId, exerciseId);
            return BuildWritingPracticeDataViewModel(exercise, sentences, attempts);
        }

        public async Task<WritingSentenceHintViewModel?> GetWritingSentenceHintAsync(int exerciseId, int sentenceId, int userId)
        {
            if (sentenceId <= 0)
            {
                return null;
            }

            var exercise = await ResolveWritingExerciseAccessAsync(exerciseId, userId);
            if (exercise == null)
            {
                return null;
            }

            var sentences = await LoadWritingSentenceRowsAsync(exerciseId);
            var sentenceIndex = sentences.FindIndex(sentence => sentence.Id == sentenceId);
            if (sentenceIndex < 0)
            {
                return null;
            }

            var sentence = sentences[sentenceIndex];

            return new WritingSentenceHintViewModel
            {
                ExerciseId = exerciseId,
                SentenceId = sentence.Id,
                SentenceNumber = sentence.SortOrder,
                HintTitle = $"Bản dịch tham khảo \u2013 Câu {sentence.SortOrder}",
                HintText = string.IsNullOrWhiteSpace(sentence.EnglishMeaning)
                    ? "Câu này hiện chưa có bản dịch tham khảo."
                    : sentence.EnglishMeaning
            };
        }

        public async Task<WritingSentenceEvaluationViewModel?> EvaluateWritingSentenceAsync(
            int exerciseId,
            int sentenceId,
            string userAnswer,
            int userId)
        {
            var exercise = await ResolveWritingExerciseAccessAsync(exerciseId, userId);
            if (exercise == null)
            {
                return null;
            }

            var sentences = await LoadWritingSentenceRowsAsync(exerciseId);
            var sentenceIndex = sentences.FindIndex(sentence => sentence.Id == sentenceId);
            if (sentenceIndex < 0)
            {
                return null;
            }

            var sentence = sentences[sentenceIndex];
            var normalizedAnswer = CollapseWhitespace(userAnswer);
            var scopeValidationFailure = TryBuildOutOfScopeSubmissionFailure(
                exerciseId,
                sentence,
                sentences,
                normalizedAnswer);
            var ruleEvaluation = scopeValidationFailure
                ?? EvaluateWritingSentenceRuleBased(
                    exerciseId,
                    sentence,
                    sentenceIndex,
                    sentences.Count,
                    normalizedAnswer);

            WritingSentenceEvaluationViewModel evaluationViewModel;

            if (ruleEvaluation.IsHardFailure)
            {
                evaluationViewModel = BuildWritingSentenceEvaluationViewModel(ruleEvaluation);
            }
            else
            {
                var aiEvaluation = await _writingAiEvaluationService.TryEvaluateSentenceAsync(
                    new WritingAiEvaluationRequest(
                        sentence.Id,
                        sentence.VietnameseText,
                        sentence.EnglishMeaning,
                        normalizedAnswer));
                evaluationViewModel = aiEvaluation == null
                    ? BuildWritingSentenceEvaluationViewModel(ruleEvaluation, sentence.EnglishMeaning)
                    : BuildWritingSentenceEvaluationViewModel(exerciseId, sentence, aiEvaluation);
            }

            await PersistWritingAttemptAsync(userId, exerciseId, sentence, normalizedAnswer, evaluationViewModel);

            var progressSummary = await LoadWritingExerciseProgressSummaryAsync(exerciseId, userId, sentences.Count);
            var firstTimeExerciseCompletion = await UpsertWritingProgressAsync(
                userId,
                exerciseId,
                sentence.Id,
                normalizedAnswer,
                evaluationViewModel.Passed,
                progressSummary,
                sentences.Count);

            if (firstTimeExerciseCompletion)
            {
                var activityUpdate = _goalsService.BuildWritingCompletionActivityUpdate();
                var activityResult = await _goalsService.RecordLearningActivityAsync(userId, activityUpdate);
                if (activityResult.Status == OperationStatus.NotFound)
                {
                    _logger.LogWarning(
                        "Writing completion activity record skipped because user {userId} was not found",
                        userId);
                }
            }

            evaluationViewModel.SentenceAttemptCount = progressSummary.SentenceAttemptCounts.GetValueOrDefault(sentence.Id);
            evaluationViewModel.ExerciseAttemptCount = progressSummary.AttemptCount;
            evaluationViewModel.CompletedSentenceCount = progressSummary.CompletedSentenceCount;
            evaluationViewModel.ExerciseStatusKey = progressSummary.StatusKey;
            evaluationViewModel.ExerciseStatusLabel = progressSummary.StatusLabel;

            return evaluationViewModel;
        }

        public async Task<WritingPracticeViewModel?> GetWritingPracticeViewModelAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            int page,
            int? exerciseId,
            int userId,
            string? status = null)
        {
            var catalog = await GetWritingExerciseCatalogAsync(selectedLevel, contentType, userId);
            var exerciseData = BuildWritingExerciseDataViewModel(catalog, topic, status);
            var exerciseListViewModel = BuildWritingExerciseListViewModel(exerciseData, page);
            var accessibleExercises = catalog.Exercises
                .Concat(catalog.MyExercises.Where(item => !item.IsLocked))
                .ToList();
            var selectedExerciseId = ResolveSelectedWritingExerciseId(exerciseId, exerciseListViewModel.Exercises, accessibleExercises);

            if (!selectedExerciseId.HasValue)
            {
                return null;
            }

            var practiceData = await GetWritingPracticeDataAsync(selectedExerciseId.Value, userId);
            if (practiceData == null)
            {
                return null;
            }

            var selectedPage = exerciseListViewModel.CurrentPage > 0
                ? exerciseListViewModel.CurrentPage
                : Math.Max(page, 1);

            return BuildWritingPracticeViewModel(exerciseListViewModel, practiceData, selectedPage);
        }

        private async Task<WritingExerciseCatalog> GetWritingExerciseCatalogAsync(
            string? selectedLevel,
            string? contentType,
            int? userId = null)
        {
            var writingIndexViewModel = BuildWritingIndexViewModel(selectedLevel);
            var selectedContentTypeKey = ResolveWritingContentTypeKey(contentType);
            var exercises = await LoadWritingExerciseCardsAsync(writingIndexViewModel.SelectedLevelKey, selectedContentTypeKey);
            List<WritingExerciseCardViewModel> myExercises = new();
            var isMyExercisesLocked = false;
            var canCreateFromAi = false;

            if (userId.HasValue)
            {
                await ApplyWritingProgressMetadataAsync(exercises, userId.Value);

                var accessContext = await ResolveWritingAccessContextAsync(userId.Value);
                canCreateFromAi = accessContext.CanCreateFromAi;
                myExercises = await LoadOwnerWritingExerciseCardsAsync(
                    writingIndexViewModel.SelectedLevelKey,
                    selectedContentTypeKey,
                    userId.Value);

                if (myExercises.Count > 0)
                {
                    isMyExercisesLocked = !accessContext.CanAccessOwnedPrivateExercises;

                    foreach (var myExercise in myExercises)
                    {
                        myExercise.IsPrivate = true;
                        myExercise.IsLocked = isMyExercisesLocked;
                        myExercise.StartActionLabel = isMyExercisesLocked
                            ? "Nâng cấp để mở khóa"
                            : "Luyện ngay";
                    }

                    if (!isMyExercisesLocked)
                    {
                        await ApplyWritingProgressMetadataAsync(myExercises, userId.Value);
                    }
                }
            }

            return new WritingExerciseCatalog(
                writingIndexViewModel.SelectedLevelKey,
                writingIndexViewModel.SelectedLevelTitle,
                selectedContentTypeKey,
                ResolveWritingContentTypeTitle(selectedContentTypeKey),
                exercises,
                myExercises,
                userId.HasValue,
                isMyExercisesLocked,
                canCreateFromAi);
        }

        private static WritingExerciseDataViewModel BuildWritingExerciseDataViewModel(
            WritingExerciseCatalog catalog,
            string? topic,
            string? status)
        {
            var normalizedTopic = NormalizeWritingTopic(topic);
            var normalizedStatus = NormalizeWritingStatus(status, catalog.HasProgressMetadata);
            var topicFilteredExercises = FilterWritingExercisesByTopic(catalog.Exercises, normalizedTopic);

            return new WritingExerciseDataViewModel
            {
                SelectedLevelKey = catalog.SelectedLevelKey,
                SelectedLevelTitle = catalog.SelectedLevelTitle,
                SelectedContentTypeKey = catalog.SelectedContentTypeKey,
                SelectedContentTypeTitle = catalog.SelectedContentTypeTitle,
                SelectedTopic = normalizedTopic,
                SelectedStatus = normalizedStatus,
                ShowProgressMetadata = catalog.HasProgressMetadata,
                TopicOptions = BuildWritingTopicOptions(catalog.Exercises.Concat(catalog.MyExercises)),
                StatusOptions = catalog.HasProgressMetadata
                    ? BuildWritingStatusOptions()
                    : new List<WritingFilterOptionViewModel>(),
                Exercises = FilterWritingExercisesByStatus(
                    topicFilteredExercises,
                    normalizedStatus,
                    catalog.HasProgressMetadata),
                MyExercises = FilterWritingExercisesByTopic(catalog.MyExercises, normalizedTopic),
                IsMyExercisesLocked = catalog.IsMyExercisesLocked,
                IsAuthenticatedUser = catalog.HasProgressMetadata,
                CanCreateFromAi = catalog.CanCreateFromAi
            };
        }

        private static WritingExerciseListViewModel BuildWritingExerciseListViewModel(
            WritingExerciseDataViewModel exerciseData,
            int page)
        {
            const int pageSize = 6;

            var filteredExercises = exerciseData.Exercises;
            var totalCount = filteredExercises.Count;
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
            var currentPage = totalPages == 0 ? 0 : Math.Clamp(page, 1, totalPages);
            var pagedItems = totalPages == 0
                ? new List<WritingExerciseCardViewModel>()
                : filteredExercises.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

            return new WritingExerciseListViewModel
            {
                SelectedLevelKey = exerciseData.SelectedLevelKey,
                SelectedLevelTitle = exerciseData.SelectedLevelTitle,
                SelectedContentTypeKey = exerciseData.SelectedContentTypeKey,
                SelectedContentTypeTitle = exerciseData.SelectedContentTypeTitle,
                SelectedTopic = exerciseData.SelectedTopic,
                SelectedStatus = exerciseData.SelectedStatus,
                ShowProgressMetadata = exerciseData.ShowProgressMetadata,
                CurrentPage = currentPage,
                TotalPages = totalPages,
                TotalCount = totalCount,
                PageSize = pageSize,
                StartItemNumber = totalPages == 0 ? 0 : ((currentPage - 1) * pageSize) + 1,
                EndItemNumber = totalPages == 0 ? 0 : Math.Min(currentPage * pageSize, totalCount),
                TopicOptions = exerciseData.TopicOptions,
                StatusOptions = exerciseData.StatusOptions,
                PageNumbers = BuildWritingPageNumbers(currentPage, totalPages),
                Exercises = pagedItems,
                MyExercises = exerciseData.MyExercises,
                IsMyExercisesLocked = exerciseData.IsMyExercisesLocked,
                IsAuthenticatedUser = exerciseData.IsAuthenticatedUser,
                CanCreateFromAi = exerciseData.CanCreateFromAi
            };
        }

        private async Task<List<WritingExerciseCardViewModel>> LoadWritingExerciseCardsAsync(string levelKey, string contentTypeKey)
        {
            return await _context.WritingExercises
                .AsNoTracking()
                .Where(exercise => exercise.UserId == null
                    && exercise.IsPublished
                    && exercise.Level == levelKey
                    && exercise.ContentType == contentTypeKey)
                .OrderBy(exercise => exercise.Id)
                .Select(exercise => new WritingExerciseCardViewModel
                {
                    Id = exercise.Id,
                    Title = exercise.Title,
                    PreviewText = exercise.PreviewText,
                    Topic = exercise.Topic,
                    SentenceCount = exercise.WritingExerciseSentences.Count()
                })
                .ToListAsync();
        }

        private async Task ApplyWritingProgressMetadataAsync(List<WritingExerciseCardViewModel> exercises, int userId)
        {
            foreach (var exercise in exercises)
            {
                exercise.StatusKey = WritingStatusNotStarted;
                exercise.StatusLabel = ResolveWritingProgressStatusLabel(WritingStatusNotStarted);
                exercise.StartActionLabel = ResolveWritingStartActionLabel(WritingStatusNotStarted);
            }

            if (exercises.Count == 0)
            {
                return;
            }

            var exerciseIds = exercises
                .Select(exercise => exercise.Id)
                .ToList();
            var attemptRows = await LoadWritingAttemptRowsAsync(userId, exerciseIds);
            var progressByExerciseId = exerciseIds.ToDictionary(
                exerciseId => exerciseId,
                exerciseId => BuildWritingExerciseProgressSummary(
                    attemptRows.Where(row => row.ExerciseId == exerciseId),
                    exercises.First(exercise => exercise.Id == exerciseId).SentenceCount));

            foreach (var exercise in exercises)
            {
                var progressSummary = progressByExerciseId.GetValueOrDefault(exercise.Id)
                    ?? BuildWritingExerciseProgressSummary(Array.Empty<WritingAttemptRow>(), exercise.SentenceCount);
                exercise.AttemptCount = progressSummary.AttemptCount;
                exercise.CompletedSentenceCount = progressSummary.CompletedSentenceCount;
                exercise.StatusKey = progressSummary.StatusKey;
                exercise.StatusLabel = progressSummary.StatusLabel;
                exercise.StartActionLabel = ResolveWritingStartActionLabel(progressSummary.StatusKey);
                exercise.LastAttemptedAtDisplay = FormatWritingTimestamp(progressSummary.LastAttemptedAtUtc);
            }
        }

        private static List<WritingExerciseCardViewModel> FilterWritingExercisesByTopic(
            IEnumerable<WritingExerciseCardViewModel> exercises,
            string normalizedTopic)
        {
            return string.Equals(normalizedTopic, "all", StringComparison.OrdinalIgnoreCase)
                ? exercises.ToList()
                : exercises
                    .Where(exercise => string.Equals(exercise.Topic, normalizedTopic, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        private static List<WritingExerciseCardViewModel> FilterWritingExercisesByStatus(
            IEnumerable<WritingExerciseCardViewModel> exercises,
            string normalizedStatus,
            bool hasProgressMetadata)
        {
            if (!hasProgressMetadata || string.Equals(normalizedStatus, WritingStatusAll, StringComparison.OrdinalIgnoreCase))
            {
                return exercises.ToList();
            }

            return exercises
                .Where(exercise => string.Equals(exercise.StatusKey, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static int? ResolveSelectedWritingExerciseId(
            int? requestedExerciseId,
            IReadOnlyCollection<WritingExerciseCardViewModel> pagedExercises,
            IReadOnlyCollection<WritingExerciseCardViewModel> catalogExercises)
        {
            if (requestedExerciseId.HasValue)
            {
                return catalogExercises.Any(exercise => exercise.Id == requestedExerciseId.Value)
                    ? requestedExerciseId.Value
                    : null;
            }

            return pagedExercises.FirstOrDefault()?.Id ?? catalogExercises.FirstOrDefault()?.Id;
        }

        private static WritingPracticeDataViewModel BuildWritingPracticeDataViewModel(
            WritingPracticeExerciseRow exercise,
            IReadOnlyList<WritingExerciseSentence> sentences,
            IReadOnlyList<WritingAttemptRow> attempts)
        {
            var progressSummary = BuildWritingExerciseProgressSummary(attempts, sentences.Count);

            return new WritingPracticeDataViewModel
            {
                SelectedLevelKey = exercise.Level,
                SelectedLevelTitle = ResolveWritingLevelTitle(exercise.Level),
                SelectedContentTypeKey = exercise.ContentType,
                SelectedContentTypeTitle = ResolveWritingContentTypeTitle(exercise.ContentType),
                ExerciseId = exercise.Id,
                ExerciseTitle = exercise.Title,
                ExercisePreviewText = exercise.PreviewText,
                ExerciseTopic = exercise.Topic,
                TotalSentenceCount = sentences.Count,
                CompletedSentenceCount = progressSummary.CompletedSentenceCount,
                AttemptCount = progressSummary.AttemptCount,
                ResumeSentenceId = ResolveResumeSentenceId(sentences, progressSummary),
                StatusKey = progressSummary.StatusKey,
                StatusLabel = progressSummary.StatusLabel,
                LastAttemptedAtDisplay = FormatWritingTimestamp(progressSummary.LastAttemptedAtUtc),
                Sentences = BuildWritingPracticeSentences(sentences, progressSummary),
                RecentAttempts = BuildWritingAttemptHistory(progressSummary.RecentAttempts)
            };
        }

        private static WritingPracticeViewModel BuildWritingPracticeViewModel(
            WritingExerciseListViewModel exerciseListViewModel,
            WritingPracticeDataViewModel practiceData,
            int selectedPage)
        {
            return new WritingPracticeViewModel
            {
                SelectedLevelKey = exerciseListViewModel.SelectedLevelKey,
                SelectedLevelTitle = exerciseListViewModel.SelectedLevelTitle,
                SelectedContentTypeKey = exerciseListViewModel.SelectedContentTypeKey,
                SelectedContentTypeTitle = exerciseListViewModel.SelectedContentTypeTitle,
                SelectedTopic = exerciseListViewModel.SelectedTopic,
                SelectedStatus = exerciseListViewModel.SelectedStatus,
                SelectedPage = selectedPage,
                ExerciseId = practiceData.ExerciseId,
                ExerciseTitle = practiceData.ExerciseTitle,
                ExercisePreviewText = practiceData.ExercisePreviewText,
                ExerciseTopic = practiceData.ExerciseTopic,
                TotalSentenceCount = practiceData.TotalSentenceCount,
                CompletedSentenceCount = practiceData.CompletedSentenceCount,
                AttemptCount = practiceData.AttemptCount,
                ResumeSentenceId = practiceData.ResumeSentenceId,
                StatusKey = practiceData.StatusKey,
                StatusLabel = practiceData.StatusLabel,
                LastAttemptedAtDisplay = practiceData.LastAttemptedAtDisplay,
                Sentences = practiceData.Sentences,
                RecentAttempts = practiceData.RecentAttempts
            };
        }

        private static WritingIndexViewModel BuildWritingIndexViewModel(string? selectedLevel)
        {
            var levels = GetWritingLevels();
            var normalizedLevel = ResolveWritingLevelKey(selectedLevel, levels);
            var selectedLevelCard = levels.Find(level => string.Equals(level.Key, normalizedLevel, StringComparison.OrdinalIgnoreCase));

            foreach (var level in levels)
            {
                level.IsSelected = string.Equals(level.Key, normalizedLevel, StringComparison.OrdinalIgnoreCase);
            }

            return new WritingIndexViewModel
            {
                SelectedLevelKey = normalizedLevel,
                SelectedLevelTitle = selectedLevelCard?.Title ?? "Cơ bản",
                Levels = levels,
                ContentTypes = GetWritingContentTypes()
            };
        }

        private static List<WritingPracticeSentenceViewModel> BuildWritingPracticeSentences(
            IReadOnlyList<WritingExerciseSentence> sentences,
            WritingExerciseProgressSummary progressSummary)
        {
            return sentences
                .Select(sentence =>
                {
                    var sentenceState = progressSummary.SentenceStates.GetValueOrDefault(sentence.Id);

                    return new WritingPracticeSentenceViewModel
                    {
                        Id = sentence.Id,
                        Number = sentence.SortOrder,
                        VietnameseText = NormalizeVietnameseDisplayText(sentence.VietnameseText),
                        Placeholder = "Nhập câu tiếng Anh cho dòng đang chọn...",
                        BreakAfter = sentence.BreakAfter,
                        AttemptCount = progressSummary.SentenceAttemptCounts.GetValueOrDefault(sentence.Id),
                        LastSubmittedAnswer = sentenceState?.LastSubmittedAnswer ?? string.Empty,
                        AcceptedAnswer = sentenceState?.AcceptedAnswer ?? string.Empty,
                        HasAccepted = sentenceState?.HasAccepted ?? false,
                        LastEvaluationPassed = sentenceState?.LastEvaluationPassed,
                        LastEvaluation = sentenceState?.LastEvaluation
                    };
                })
                .ToList();
        }

        // BuildHintTitle removed – hint titles are now the stable format
        //   "Bản dịch tham khảo – Câu N" generated directly in GetWritingSentenceHintAsync.

        // BuildSentenceTypeHint is used only by EvaluateWritingSentenceRuleBased to compose
        // MeaningFeedback copy. It is NOT used by the public hint endpoint.
        private static string BuildSentenceTypeHint(string vietnameseText, int index, int totalCount)
        {
            if (IsSenderNameLine(index, totalCount))
            {
                return "Hãy giữ nguyên tên người gửi khi viết sang tiếng Anh.";
            }

            if (IsGreetingLine(index))
            {
                return "Hãy dùng một lời chào ngắn gọn, tự nhiên và phù hợp với giọng điệu của email.";
            }

            if (IsClosingLine(vietnameseText, index, totalCount))
            {
                return "Hãy dùng một câu kết tiếng Anh tự nhiên cho cuối email.";
            }

            if (vietnameseText.Contains('?'))
            {
                return "Hãy giữ dòng này thành một câu hỏi trực tiếp và tự nhiên trong tiếng Anh.";
            }

            return "Hãy dịch rõ dòng đang được chọn và giữ giọng điệu tự nhiên cho toàn bài.";
        }

        private static bool IsGreetingLine(int index)
        {
            return index == 0;
        }

        private static bool IsSenderNameLine(int index, int totalCount)
        {
            return index == totalCount - 1;
        }

        private static bool IsClosingLine(string vietnameseText, int index, int totalCount)
        {
            var normalizedVietnameseText = NormalizeVietnameseDisplayText(vietnameseText);
            var hasClosingPhrase = normalizedVietnameseText.Contains("Tran trong", StringComparison.OrdinalIgnoreCase)
                || normalizedVietnameseText.Contains("Chuc may man", StringComparison.OrdinalIgnoreCase);

            return !IsGreetingLine(index)
                && !IsSenderNameLine(index, totalCount)
                && (index == totalCount - 2
                    || hasClosingPhrase);
        }

        private static string ResolveWritingLevelKey(string? selectedLevel, List<WritingLevelCardViewModel> levels)
        {
            var matchedLevel = levels.Find(level => string.Equals(level.Key, selectedLevel, StringComparison.OrdinalIgnoreCase));
            return matchedLevel?.Key ?? "beginner";
        }

        private static string ResolveWritingLevelTitle(string? selectedLevel)
        {
            var matchedLevel = GetWritingLevels()
                .Find(level => string.Equals(level.Key, selectedLevel, StringComparison.OrdinalIgnoreCase));

            return matchedLevel?.Title ?? "Cơ bản";
        }

        private static string ResolveWritingContentTypeKey(string? contentType)
        {
            var matchedContentType = GetWritingContentTypes()
                .Find(type => string.Equals(type.Key, contentType, StringComparison.OrdinalIgnoreCase));

            return matchedContentType?.Key ?? "emails";
        }

        private static string ResolveWritingContentTypeTitle(string? contentType)
        {
            var matchedContentType = GetWritingContentTypes()
                .Find(type => string.Equals(type.Key, contentType, StringComparison.OrdinalIgnoreCase));

            return matchedContentType?.Title ?? "Luyện tập chung";
        }

        private static List<WritingLevelCardViewModel> GetWritingLevels()
        {
            return new List<WritingLevelCardViewModel>
            {
                new()
                {
                    Key = "beginner",
                    Title = "Cơ bản",
                    Description = "Phù hợp cho người mới bắt đầu làm quen với các đoạn văn ngắn và đơn giản.",
                    DurationText = "15-20 phút mỗi bài",
                    IconClass = "fas fa-seedling",
                    AccentColor = "#24b36b"
                },
                new()
                {
                    Key = "intermediate",
                    Title = "Trung cấp",
                    Description = "Dành cho người học muốn nối ý tốt hơn với nhiều chi tiết và từ vựng hơn.",
                    DurationText = "20-30 phút mỗi bài",
                    IconClass = "far fa-star",
                    AccentColor = "#2d7ff9"
                },
                new()
                {
                    Key = "advanced",
                    Title = "Nâng cao",
                    Description = "Thử sức với cấu trúc mạch lạc hơn, diễn đạt tinh hơn và giọng văn chuyên nghiệp hơn.",
                    DurationText = "30-40 phút mỗi bài",
                    IconClass = "fas fa-crown",
                    AccentColor = "#9a5cf5"
                }
            };
        }

        private static List<WritingContentTypeCardViewModel> GetWritingContentTypes()
        {
            return new List<WritingContentTypeCardViewModel>
            {
                new()
                {
                    Key = "emails",
                    Title = "Email",
                    Description = "Thư từ công việc và cá nhân",
                    IconClass = "far fa-envelope",
                    AccentColor = "#f59e0b"
                },
                new()
                {
                    Key = "diaries",
                    Title = "Nhật ký",
                    Description = "Viết cá nhân mang tính ghi chép, suy ngẫm",
                    IconClass = "far fa-calendar",
                    AccentColor = "#24b36b"
                },
                new()
                {
                    Key = "essays",
                    Title = "Bài luận",
                    Description = "Đoạn văn nêu quan điểm hoặc mang phong cách học thuật",
                    IconClass = "fas fa-graduation-cap",
                    AccentColor = "#2d7ff9"
                },
                new()
                {
                    Key = "articles",
                    Title = "Bài viết",
                    Description = "Viết thông tin theo bố cục rõ ràng",
                    IconClass = "far fa-newspaper",
                    AccentColor = "#ef4444"
                },
                new()
                {
                    Key = "stories",
                    Title = "Câu chuyện",
                    Description = "Bài kể chuyện và đoạn văn sáng tạo",
                    IconClass = "fas fa-book-open",
                    AccentColor = "#8b5cf6"
                },
                new()
                {
                    Key = "reports",
                    Title = "Báo cáo",
                    Description = "Tóm tắt rõ ràng và lối viết thiên về thông tin thực tế",
                    IconClass = "far fa-file-alt",
                    AccentColor = "#06b6d4"
                }
            };
        }

        private static List<WritingFilterOptionViewModel> BuildWritingTopicOptions(
            IEnumerable<WritingExerciseCardViewModel> exercises)
        {
            var options = new List<WritingFilterOptionViewModel>
            {
                new()
                {
                    Value = "all",
                    Label = "Tất cả chủ đề"
                }
            };

            options.AddRange(exercises
                .Where(exercise => !string.IsNullOrWhiteSpace(exercise.Topic))
                .Select(exercise => exercise.Topic)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(topic => topic, StringComparer.OrdinalIgnoreCase)
                .Select(topic => new WritingFilterOptionViewModel
                {
                    Value = topic,
                    Label = topic
                }));

            return options;
        }

        private static List<WritingFilterOptionViewModel> BuildWritingStatusOptions()
        {
            return new List<WritingFilterOptionViewModel>
            {
                new()
                {
                    Value = WritingStatusAll,
                    Label = "Tất cả trạng thái"
                },
                new()
                {
                    Value = WritingStatusNotStarted,
                    Label = "Chưa bắt đầu"
                },
                new()
                {
                    Value = WritingStatusInProgress,
                    Label = "Đang luyện"
                },
                new()
                {
                    Value = WritingStatusCompleted,
                    Label = "Hoàn thành"
                }
            };
        }

        private static string NormalizeWritingTopic(string? topic)
        {
            return string.IsNullOrWhiteSpace(topic) ? WritingStatusAll : topic.Trim();
        }

        private static string NormalizeWritingStatus(string? status, bool hasProgressMetadata)
        {
            if (!hasProgressMetadata || string.IsNullOrWhiteSpace(status))
            {
                return WritingStatusAll;
            }

            return status.Trim().ToLowerInvariant() switch
            {
                WritingStatusNotStarted => WritingStatusNotStarted,
                WritingStatusInProgress => WritingStatusInProgress,
                WritingStatusCompleted => WritingStatusCompleted,
                _ => WritingStatusAll
            };
        }

        private static List<int?> BuildWritingPageNumbers(int currentPage, int totalPages)
        {
            var pages = new List<int?>();

            if (totalPages <= 7)
            {
                for (var page = 1; page <= totalPages; page++)
                {
                    pages.Add(page);
                }

                return pages;
            }

            if (currentPage <= 4)
            {
                pages.AddRange(new int?[] { 1, 2, 3, 4, null, totalPages });
                return pages;
            }

            if (currentPage >= totalPages - 3)
            {
                pages.AddRange(new int?[] { 1, null, totalPages - 3, totalPages - 2, totalPages - 1, totalPages });
                return pages;
            }

            pages.AddRange(new int?[]
            {
                1,
                null,
                currentPage - 1,
                currentPage,
                currentPage + 1,
                null,
                totalPages
            });

            return pages;
        }

        private async Task<List<WritingAttemptRow>> LoadWritingAttemptRowsAsync(int userId, IReadOnlyCollection<int> exerciseIds)
        {
            if (exerciseIds.Count == 0)
            {
                return new List<WritingAttemptRow>();
            }

            return await _context.UserWritingAttempts
                .AsNoTracking()
                .Where(attempt => attempt.UserId == userId && exerciseIds.Contains(attempt.WritingExerciseId))
                .OrderByDescending(attempt => attempt.CreatedAtUtc)
                .Select(attempt => new WritingAttemptRow
                {
                    ExerciseId = attempt.WritingExerciseId,
                    SentenceId = attempt.WritingExerciseSentenceId,
                    SentenceNumber = attempt.WritingExerciseSentence.SortOrder,
                    SubmittedAnswer = attempt.SubmittedAnswer,
                    Passed = attempt.Passed,
                    UsedAi = attempt.UsedAi,
                    EvaluationSource = attempt.EvaluationSource,
                    SummaryTitle = attempt.SummaryTitle,
                    SummaryText = attempt.SummaryText,
                    ReviewText = attempt.ReviewText,
                    MeaningFeedback = attempt.MeaningFeedback,
                    GrammarFeedback = attempt.GrammarFeedback,
                    NaturalnessFeedback = attempt.NaturalnessFeedback,
                    WordChoiceFeedback = attempt.WordChoiceFeedback,
                    SuggestedRewrite = attempt.SuggestedRewrite,
                    CreatedAtUtc = attempt.CreatedAtUtc
                })
                .ToListAsync();
        }

        private Task<List<WritingAttemptRow>> LoadWritingAttemptRowsAsync(int userId, int exerciseId)
        {
            return LoadWritingAttemptRowsAsync(userId, new[] { exerciseId });
        }

        private async Task PersistWritingAttemptAsync(
            int userId,
            int exerciseId,
            WritingSentenceLookupRow sentence,
            string normalizedAnswer,
            WritingSentenceEvaluationViewModel evaluationViewModel)
        {
            _context.UserWritingAttempts.Add(new UserWritingAttempt
            {
                UserId = userId,
                WritingExerciseId = exerciseId,
                WritingExerciseSentenceId = sentence.Id,
                SubmittedAnswer = normalizedAnswer,
                Passed = evaluationViewModel.Passed,
                UsedAi = evaluationViewModel.UsedAi,
                EvaluationSource = evaluationViewModel.EvaluationSource,
                SummaryTitle = NormalizeSnapshotText(evaluationViewModel.SummaryTitle, SummaryTitleMaxLength),
                SummaryText = NormalizeSnapshotText(evaluationViewModel.SummaryText, SummaryTextMaxLength),
                ReviewText = NormalizeSnapshotText(evaluationViewModel.ReviewText, ReviewTextMaxLength, preserveLineBreaks: true),
                MeaningFeedback = NormalizeSnapshotText(evaluationViewModel.MeaningFeedback, FeedbackFieldMaxLength),
                GrammarFeedback = NormalizeSnapshotText(evaluationViewModel.GrammarFeedback, FeedbackFieldMaxLength),
                NaturalnessFeedback = NormalizeSnapshotText(evaluationViewModel.NaturalnessFeedback, FeedbackFieldMaxLength),
                WordChoiceFeedback = NormalizeSnapshotText(evaluationViewModel.WordChoiceFeedback, FeedbackFieldMaxLength),
                SuggestedRewrite = NormalizeSnapshotText(evaluationViewModel.SuggestedRewrite, SuggestedRewriteMaxLength),
                CreatedAtUtc = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        private async Task<bool> UpsertWritingProgressAsync(
            int userId,
            int exerciseId,
            int sentenceId,
            string normalizedAnswer,
            bool passed,
            WritingExerciseProgressSummary progressSummary,
            int totalSentenceCount)
        {
            var utcNow = DateTime.UtcNow;
            var sentenceProgress = await _context.UserWritingSentenceProgresses
                .FirstOrDefaultAsync(progress =>
                    progress.UserId == userId
                    && progress.SentenceId == sentenceId);

            if (sentenceProgress == null)
            {
                sentenceProgress = new UserWritingSentenceProgress
                {
                    UserId = userId,
                    WritingExerciseId = exerciseId,
                    SentenceId = sentenceId,
                    AttemptCount = 1,
                    IsPassed = passed,
                    AcceptedAnswer = passed ? normalizedAnswer : null,
                    LastAttemptAt = utcNow,
                    PassedAt = passed ? utcNow : null
                };
                _context.UserWritingSentenceProgresses.Add(sentenceProgress);
            }
            else
            {
                sentenceProgress.AttemptCount += 1;
                sentenceProgress.LastAttemptAt = utcNow;

                if (passed)
                {
                    sentenceProgress.IsPassed = true;
                    sentenceProgress.AcceptedAnswer = normalizedAnswer;
                    sentenceProgress.PassedAt ??= utcNow;
                }
            }

            var exerciseProgress = await _context.UserWritingExerciseProgresses
                .FirstOrDefaultAsync(progress =>
                    progress.UserId == userId
                    && progress.WritingExerciseId == exerciseId);

            var wasCompleted = exerciseProgress?.IsCompleted ?? false;
            var isCompleted = totalSentenceCount > 0
                && progressSummary.CompletedSentenceCount >= totalSentenceCount;

            if (exerciseProgress == null)
            {
                exerciseProgress = new UserWritingExerciseProgress
                {
                    UserId = userId,
                    WritingExerciseId = exerciseId,
                    TotalSentenceCount = totalSentenceCount,
                    PassedSentenceCount = progressSummary.CompletedSentenceCount,
                    AttemptCount = progressSummary.AttemptCount,
                    IsCompleted = isCompleted,
                    LastAttemptAt = progressSummary.LastAttemptedAtUtc ?? utcNow,
                    CompletedAt = isCompleted ? utcNow : null
                };
                _context.UserWritingExerciseProgresses.Add(exerciseProgress);
            }
            else
            {
                exerciseProgress.TotalSentenceCount = totalSentenceCount;
                exerciseProgress.PassedSentenceCount = progressSummary.CompletedSentenceCount;
                exerciseProgress.AttemptCount = progressSummary.AttemptCount;
                exerciseProgress.IsCompleted = isCompleted;
                exerciseProgress.LastAttemptAt = progressSummary.LastAttemptedAtUtc ?? utcNow;
                exerciseProgress.CompletedAt = isCompleted
                    ? exerciseProgress.CompletedAt ?? utcNow
                    : null;
            }

            await _context.SaveChangesAsync();

            return !wasCompleted && isCompleted;
        }

        private async Task<WritingExerciseProgressSummary> LoadWritingExerciseProgressSummaryAsync(
            int exerciseId,
            int userId,
            int totalSentenceCount)
        {
            var attemptRows = await LoadWritingAttemptRowsAsync(userId, exerciseId);
            return BuildWritingExerciseProgressSummary(attemptRows, totalSentenceCount);
        }

        private static WritingExerciseProgressSummary BuildWritingExerciseProgressSummary(
            IEnumerable<WritingAttemptRow> attempts,
            int totalSentenceCount)
        {
            var attemptList = attempts
                .OrderByDescending(attempt => attempt.CreatedAtUtc)
                .ToList();

            var progressSummary = new WritingExerciseProgressSummary
            {
                AttemptCount = attemptList.Count,
                LastAttemptedAtUtc = attemptList.FirstOrDefault()?.CreatedAtUtc,
                LatestAttemptSentenceId = attemptList.FirstOrDefault()?.SentenceId,
                RecentAttempts = attemptList.Take(5).ToList()
            };

            foreach (var sentenceAttemptGroup in attemptList.GroupBy(attempt => attempt.SentenceId))
            {
                var latestAttempt = sentenceAttemptGroup
                    .OrderByDescending(attempt => attempt.CreatedAtUtc)
                    .First();
                var latestAcceptedAttempt = sentenceAttemptGroup
                    .Where(attempt => attempt.Passed)
                    .OrderByDescending(attempt => attempt.CreatedAtUtc)
                    .FirstOrDefault();

                progressSummary.SentenceAttemptCounts[sentenceAttemptGroup.Key] = sentenceAttemptGroup.Count();
                progressSummary.SentenceStates[sentenceAttemptGroup.Key] = new WritingSentenceProgressState
                {
                    LastSubmittedAnswer = latestAttempt.SubmittedAnswer,
                    AcceptedAnswer = latestAcceptedAttempt?.SubmittedAnswer ?? string.Empty,
                    HasAccepted = latestAcceptedAttempt != null,
                    LastEvaluationPassed = latestAttempt.Passed,
                    LastEvaluation = BuildEvaluationSnapshot(latestAttempt)
                };
            }

            progressSummary.CompletedSentenceCount = progressSummary.SentenceStates.Values.Count(state => state.HasAccepted);
            progressSummary.StatusKey = ResolveWritingProgressStatusKey(
                progressSummary.AttemptCount,
                progressSummary.CompletedSentenceCount,
                totalSentenceCount);
            progressSummary.StatusLabel = ResolveWritingProgressStatusLabel(progressSummary.StatusKey);

            return progressSummary;
        }

        private static int ResolveResumeSentenceId(
            IReadOnlyList<WritingExerciseSentence> sentences,
            WritingExerciseProgressSummary progressSummary)
        {
            var firstIncompleteSentenceId = sentences
                .FirstOrDefault(sentence =>
                    !progressSummary.SentenceStates.TryGetValue(sentence.Id, out var sentenceState)
                    || !sentenceState.HasAccepted)
                ?.Id;

            return firstIncompleteSentenceId
                ?? progressSummary.LatestAttemptSentenceId
                ?? sentences.First().Id;
        }

        private static List<WritingAttemptHistoryItemViewModel> BuildWritingAttemptHistory(
            IReadOnlyList<WritingAttemptRow> recentAttempts)
        {
            return recentAttempts
                .Select(attempt => new WritingAttemptHistoryItemViewModel
                {
                    SentenceId = attempt.SentenceId,
                    SentenceNumber = attempt.SentenceNumber,
                    Passed = attempt.Passed,
                    StatusLabel = attempt.Passed ? "Đạt" : "Cần sửa",
                    SubmittedAnswerPreview = BuildWritingAttemptPreview(attempt.SubmittedAnswer),
                    EvaluationSource = attempt.EvaluationSource,
                    TimestampDisplay = FormatWritingTimestamp(attempt.CreatedAtUtc)
                })
                .ToList();
        }

        private static string BuildWritingAttemptPreview(string submittedAnswer)
        {
            var normalizedAnswer = CollapseWhitespace(submittedAnswer);

            if (normalizedAnswer.Length <= 72)
            {
                return normalizedAnswer;
            }

            return normalizedAnswer[..69] + "...";
        }

        private static string ResolveWritingProgressStatusKey(
            int attemptCount,
            int completedSentenceCount,
            int totalSentenceCount)
        {
            if (attemptCount <= 0)
            {
                return WritingStatusNotStarted;
            }

            return totalSentenceCount > 0 && completedSentenceCount >= totalSentenceCount
                ? WritingStatusCompleted
                : WritingStatusInProgress;
        }

        private static string ResolveWritingProgressStatusLabel(string statusKey)
        {
            return statusKey switch
            {
                WritingStatusCompleted => "Hoàn thành",
                WritingStatusInProgress => "Đang luyện",
                _ => "Chưa bắt đầu"
            };
        }

        private static string ResolveWritingStartActionLabel(string statusKey)
        {
            return statusKey switch
            {
                WritingStatusCompleted => "Xem lại",
                WritingStatusInProgress => "Tiếp tục",
                _ => "Bắt đầu"
            };
        }

        private static string FormatWritingTimestamp(DateTime? timestampUtc)
        {
            return timestampUtc.HasValue
                ? timestampUtc.Value.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private async Task<List<WritingSentenceLookupRow>> LoadWritingSentenceRowsAsync(int exerciseId)
        {
            var sentenceRows = await _context.WritingExerciseSentences
                .AsNoTracking()
                .Where(sentence => sentence.WritingExerciseId == exerciseId)
                .OrderBy(sentence => sentence.SortOrder)
                .Select(sentence => new WritingSentenceLookupRow
                {
                    Id = sentence.Id,
                    SortOrder = sentence.SortOrder,
                    VietnameseText = sentence.VietnameseText,
                    EnglishMeaning = sentence.EnglishMeaning
                })
                .ToListAsync();

            foreach (var sentenceRow in sentenceRows)
            {
                sentenceRow.VietnameseText = NormalizeVietnameseDisplayText(sentenceRow.VietnameseText);
            }

            return sentenceRows;
        }

        private static WritingRuleEvaluationResult? TryBuildOutOfScopeSubmissionFailure(
            int exerciseId,
            WritingSentenceLookupRow sentence,
            IReadOnlyList<WritingSentenceLookupRow> allSentences,
            string userAnswer)
        {
            var answerTokens = ExtractComparisonTokens(userAnswer);
            if (answerTokens.Count == 0)
            {
                return null;
            }

            var referenceTokens = ExtractComparisonTokens(sentence.EnglishMeaning);
            var answerDistinctTokens = answerTokens
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var currentMatch = BuildSentenceContentOverlap(sentence, answerDistinctTokens);
            var otherMatches = allSentences
                .Where(candidate => candidate.Id != sentence.Id)
                .Select(candidate => BuildSentenceContentOverlap(candidate, answerDistinctTokens))
                .Where(match => match.MatchedContentTokens > 0)
                .OrderByDescending(match => match.Coverage)
                .ThenByDescending(match => match.MatchedContentTokens)
                .ToList();
            var bestOtherMatch = otherMatches.FirstOrDefault();
            var hasStrongOtherMatch = otherMatches.Any(match => match.Coverage >= 0.55d && match.MatchedContentTokens >= 2);

            var looksLikeMultipleSentences = CountSentenceLikeSegments(userAnswer) >= 2
                && answerTokens.Count >= Math.Max(referenceTokens.Count + 2, 5);
            var looksOverlongForSingleSentence = referenceTokens.Count > 0
                && answerTokens.Count >= Math.Max(referenceTokens.Count + 8, (int)Math.Ceiling(referenceTokens.Count * 2.6d));
            var looksLikePastedPassage = !looksLikeMultipleSentences
                && looksOverlongForSingleSentence
                && hasStrongOtherMatch;
            var looksLikeAnotherSentence = bestOtherMatch != null
                && bestOtherMatch.Coverage >= 0.85d
                && bestOtherMatch.MatchedContentTokens >= 2
                && bestOtherMatch.MatchedContentTokens >= currentMatch.MatchedContentTokens + 2;

            if (looksLikeMultipleSentences || looksLikePastedPassage)
            {
                return new WritingRuleEvaluationResult
                {
                    ExerciseId = exerciseId,
                    SentenceId = sentence.Id,
                    SentenceNumber = sentence.SortOrder,
                    Passed = false,
                    CanAutoAdvance = false,
                    UsedAi = false,
                    EvaluationSource = "rule-based",
                    IsHardFailure = true,
                    SummaryTitle = "Chỉ gửi một câu mỗi lần",
                    SummaryText = "Câu trả lời này có vẻ đang chứa nhiều hơn một câu hoặc kéo sang phần khác của bài. Hãy chỉ gửi đúng câu đang được chọn.",
                    MeaningFeedback = "Bài Writing này được chấm theo từng câu, nên hệ thống chưa thể chấp nhận nội dung gồm câu khác hoặc cả đoạn.",
                    GrammarFeedback = "Hãy tách riêng đúng một câu tiếng Anh cho dòng hiện tại trước khi gửi lại.",
                    NaturalnessFeedback = "Giữ câu trả lời gọn trong phạm vi của đúng câu đang được tô sáng.",
                    WordChoiceFeedback = "Chỉ dùng ý và từ vựng cần thiết để dịch câu hiện tại, không gộp thêm câu trước hoặc câu sau.",
                    SuggestedRewrite = string.Empty
                };
            }

            if (!looksLikeAnotherSentence)
            {
                return null;
            }

            var meaningFeedback = bestOtherMatch == null
                ? "Nội dung vừa gửi chưa bám đúng câu hiện tại. Hãy dịch lại đúng dòng đang được chọn."
                : $"Nội dung vừa gửi đang giống câu {bestOtherMatch.SentenceNumber} hơn câu {sentence.SortOrder}. Hãy dịch đúng dòng đang được tô sáng.";

            return new WritingRuleEvaluationResult
            {
                ExerciseId = exerciseId,
                SentenceId = sentence.Id,
                SentenceNumber = sentence.SortOrder,
                Passed = false,
                CanAutoAdvance = false,
                UsedAi = false,
                EvaluationSource = "rule-based",
                IsHardFailure = true,
                SummaryTitle = "Bạn đang trả lời sang câu khác",
                SummaryText = $"Câu trả lời này có vẻ khớp với một câu khác trong bài hơn là câu {sentence.SortOrder}. Hãy quay lại đúng câu đang được chọn.",
                MeaningFeedback = meaningFeedback,
                GrammarFeedback = "Hãy chỉ gửi một câu tiếng Anh cho đúng dòng hiện tại.",
                NaturalnessFeedback = "Sau khi đổi sang câu khác trên màn hình, hãy cập nhật lại câu trả lời cho khớp.",
                WordChoiceFeedback = "Đừng gộp hoặc chuyển ý từ câu liền trước hay câu liền sau vào câu hiện tại.",
                SuggestedRewrite = string.Empty
            };
        }

        private static WritingSentenceContentOverlap BuildSentenceContentOverlap(
            WritingSentenceLookupRow sentence,
            IReadOnlyCollection<string> answerDistinctTokens)
        {
            var referenceContentTokens = ExtractMeaningfulComparisonTokens(sentence.EnglishMeaning);
            var matchedContentTokens = referenceContentTokens
                .Intersect(answerDistinctTokens, StringComparer.OrdinalIgnoreCase)
                .Count();
            var coverage = referenceContentTokens.Count == 0
                ? 0d
                : matchedContentTokens / (double)referenceContentTokens.Count;

            return new WritingSentenceContentOverlap
            {
                SentenceId = sentence.Id,
                SentenceNumber = sentence.SortOrder,
                MatchedContentTokens = matchedContentTokens,
                Coverage = coverage
            };
        }

        private static WritingRuleEvaluationResult EvaluateWritingSentenceRuleBased(
            int exerciseId,
            WritingSentenceLookupRow sentence,
            int sentenceIndex,
            int totalSentenceCount,
            string userAnswer)
        {
            var normalizedReference = NormalizeForComparison(sentence.EnglishMeaning);
            var normalizedAnswer = NormalizeForComparison(userAnswer);
            var referenceTokens = ExtractComparisonTokens(sentence.EnglishMeaning);
            var answerTokens = ExtractComparisonTokens(userAnswer);

            if (string.IsNullOrWhiteSpace(userAnswer))
            {
                return new WritingRuleEvaluationResult
                {
                    ExerciseId = exerciseId,
                    SentenceId = sentence.Id,
                    SentenceNumber = sentence.SortOrder,
                    Passed = false,
                    CanAutoAdvance = false,
                    UsedAi = false,
                    EvaluationSource = "rule-based",
                    IsHardFailure = true,
                    SummaryTitle = "Cần nhập câu",
                    SummaryText = "Hãy nhập một câu tiếng Anh trước khi yêu cầu hệ thống chấm.",
                    MeaningFeedback = "Hiện chưa có câu tiếng Anh để đối chiếu.",
                    GrammarFeedback = "Hãy bắt đầu bằng một câu tiếng Anh hoàn chỉnh.",
                    NaturalnessFeedback = "Hãy giữ cách diễn đạt ngắn gọn và rõ ràng.",
                    WordChoiceFeedback = "Hãy dùng các ý chính từ câu tiếng Việt.",
                    SuggestedRewrite = string.Empty
                };
            }

            if (answerTokens.Count == 0)
            {
                return new WritingRuleEvaluationResult
                {
                    ExerciseId = exerciseId,
                    SentenceId = sentence.Id,
                    SentenceNumber = sentence.SortOrder,
                    Passed = false,
                    CanAutoAdvance = false,
                    UsedAi = false,
                    EvaluationSource = "rule-based",
                    IsHardFailure = true,
                    SummaryTitle = "Cần có từ tiếng Anh",
                    SummaryText = "Hãy dùng ít nhất một từ tiếng Anh để hệ thống có thể chấm.",
                    MeaningFeedback = "Câu trả lời hiện tại chưa có đủ nội dung tiếng Anh để kiểm tra ý nghĩa.",
                    GrammarFeedback = "Hãy viết một câu hoàn chỉnh thay vì chỉ nhập ký hiệu hoặc để trống.",
                    NaturalnessFeedback = "Hãy hướng tới một câu tiếng Anh tự nhiên.",
                    WordChoiceFeedback = "Hãy chọn từ vựng sát với ý gốc.",
                    SuggestedRewrite = string.Empty
                };
            }

            var referenceContentTokens = referenceTokens
                .Where(token => !CommonEnglishStopWords.Contains(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var answerDistinctTokens = answerTokens
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var matchedContentTokens = referenceContentTokens
                .Intersect(answerDistinctTokens, StringComparer.OrdinalIgnoreCase)
                .Count();

            var contentCoverage = referenceContentTokens.Count == 0
                ? 1d
                : matchedContentTokens / (double)referenceContentTokens.Count;

            var lengthRatio = referenceTokens.Count == 0
                ? 1d
                : answerTokens.Count / (double)referenceTokens.Count;

            var exactMatch = string.Equals(normalizedAnswer, normalizedReference, StringComparison.Ordinal);
            var shortFormPass = answerTokens.Count <= 3 && referenceTokens.Count <= 3 && contentCoverage >= 0.75;
            var structuralPass = contentCoverage >= 0.4 && lengthRatio is >= 0.45 and <= 2.5;
            var fallbackPass = exactMatch || shortFormPass || structuralPass;

            var sentenceTypeHint = BuildSentenceTypeHint(sentence.VietnameseText, sentenceIndex, totalSentenceCount);
            var grammarFeedback = BuildGrammarFeedback(userAnswer, fallbackPass);
            var naturalnessFeedback = fallbackPass
                ? "Cấu trúc câu này tạm ổn và đủ rõ để bạn đi tiếp."
                : "Hãy bám sát ý gốc hơn và diễn đạt thành một câu tiếng Anh tự nhiên.";
            var wordChoiceFeedback = matchedContentTokens > 0
                ? "Bạn đã dùng được một vài từ khóa gần với ý cần diễn đạt, đó là khởi đầu tốt."
                : "Hãy thử dựa vào động từ chính hoặc danh từ quan trọng từ câu tiếng Việt.";

            return new WritingRuleEvaluationResult
            {
                ExerciseId = exerciseId,
                SentenceId = sentence.Id,
                SentenceNumber = sentence.SortOrder,
                Passed = fallbackPass,
                CanAutoAdvance = fallbackPass,
                UsedAi = false,
                EvaluationSource = "rule-based",
                IsHardFailure = false,
                SummaryTitle = fallbackPass ? "Câu đạt yêu cầu" : "Hãy thử lại câu này",
                SummaryText = fallbackPass
                    ? "Đánh giá nhanh của hệ thống đã chấp nhận câu này. Bạn có thể sang dòng tiếp theo."
                    : "Đánh giá nhanh của hệ thống chưa chấp nhận câu này. Hãy chỉnh lại ý và thử lại.",
                MeaningFeedback = fallbackPass
                    ? "Câu trả lời có vẻ đã đủ gần với ý gốc để vượt qua bước kiểm tra nhanh của hệ thống."
                    : $"Câu trả lời có thể vẫn chưa sát ý gốc. Gợi ý: {sentenceTypeHint}",
                GrammarFeedback = grammarFeedback,
                NaturalnessFeedback = naturalnessFeedback,
                WordChoiceFeedback = wordChoiceFeedback,
                SuggestedRewrite = string.Empty
            };
        }

        private static WritingSentenceEvaluationViewModel BuildWritingSentenceEvaluationViewModel(
            int exerciseId,
            WritingSentenceLookupRow sentence,
            WritingAiEvaluationResult aiEvaluation)
        {
            var finalPassed = aiEvaluation.Passed;
            var suggestedRewrite = ResolveSuggestedRewrite(
                sentence.EnglishMeaning,
                aiEvaluation.Passed,
                aiEvaluation.SuggestedRewrite);
            var summaryTitle = finalPassed ? "Câu đạt yêu cầu" : "Hãy sửa lại câu này";
            var summaryText = finalPassed
                ? "Hệ thống đã chấp nhận câu dịch này. Bạn có thể chuyển sang câu tiếp theo."
                : "Ý nghĩa hoặc cách diễn đạt của câu này vẫn cần chỉnh thêm trước khi được đánh dấu hoàn thành.";

            return new WritingSentenceEvaluationViewModel
            {
                ExerciseId = exerciseId,
                SentenceId = sentence.Id,
                SentenceNumber = sentence.SortOrder,
                Passed = finalPassed,
                CanAutoAdvance = finalPassed,
                UsedAi = true,
                EvaluationSource = "ai",
                SummaryTitle = summaryTitle,
                SummaryText = summaryText,
                ReviewText = BuildReviewText(
                    aiEvaluation.OverallFeedback,
                    aiEvaluation.MeaningFeedback,
                    aiEvaluation.GrammarFeedback,
                    aiEvaluation.NaturalnessFeedback,
                    aiEvaluation.WordChoiceFeedback),
                MeaningFeedback = aiEvaluation.MeaningFeedback,
                GrammarFeedback = aiEvaluation.GrammarFeedback,
                NaturalnessFeedback = aiEvaluation.NaturalnessFeedback,
                WordChoiceFeedback = aiEvaluation.WordChoiceFeedback,
                SuggestedRewrite = suggestedRewrite
            };
        }

        private static WritingSentenceEvaluationViewModel BuildWritingSentenceEvaluationViewModel(
            WritingRuleEvaluationResult evaluation,
            string? referenceAnswer = null)
        {
            var suggestedRewrite = referenceAnswer == null
                ? evaluation.SuggestedRewrite
                : ResolveSuggestedRewrite(referenceAnswer, evaluation.Passed, evaluation.SuggestedRewrite);

            return new WritingSentenceEvaluationViewModel
            {
                ExerciseId = evaluation.ExerciseId,
                SentenceId = evaluation.SentenceId,
                SentenceNumber = evaluation.SentenceNumber,
                Passed = evaluation.Passed,
                CanAutoAdvance = evaluation.CanAutoAdvance,
                UsedAi = evaluation.UsedAi,
                EvaluationSource = evaluation.EvaluationSource,
                SummaryTitle = evaluation.SummaryTitle,
                SummaryText = evaluation.SummaryText,
                ReviewText = BuildReviewText(
                    evaluation.SummaryText,
                    evaluation.MeaningFeedback,
                    evaluation.GrammarFeedback,
                    evaluation.NaturalnessFeedback,
                    evaluation.WordChoiceFeedback),
                MeaningFeedback = evaluation.MeaningFeedback,
                GrammarFeedback = evaluation.GrammarFeedback,
                NaturalnessFeedback = evaluation.NaturalnessFeedback,
                WordChoiceFeedback = evaluation.WordChoiceFeedback,
                SuggestedRewrite = suggestedRewrite
            };
        }

        private static string BuildReviewText(
            string overallFeedback,
            string meaningFeedback,
            string grammarFeedback,
            string naturalnessFeedback,
            string wordChoiceFeedback)
        {
            var reviewLines = new[]
            {
                BuildReviewLine("Tổng quan", overallFeedback),
                BuildReviewLine("Ý nghĩa", meaningFeedback),
                BuildReviewLine("Ngữ pháp", grammarFeedback),
                BuildReviewLine("Độ tự nhiên", naturalnessFeedback),
                BuildReviewLine("Từ vựng", wordChoiceFeedback)
            }
            .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join("\n", reviewLines);
        }

        private static string BuildReviewLine(string label, string value)
        {
            var normalizedValue = CollapseWhitespace(value);
            return string.IsNullOrWhiteSpace(normalizedValue)
                ? string.Empty
                : $"{label}: {normalizedValue}";
        }

        private static string ResolveSuggestedRewrite(
            string referenceAnswer,
            bool passed,
            string suggestedRewrite)
        {
            if (passed)
            {
                return string.Empty;
            }

            var normalizedRewrite = CollapseWhitespace(suggestedRewrite);
            if (IsMeaningfulSuggestedRewrite(normalizedRewrite))
            {
                return normalizedRewrite;
            }

            var normalizedReference = CollapseWhitespace(referenceAnswer);
            return !string.IsNullOrWhiteSpace(normalizedReference)
                ? normalizedReference
                : "Try again with a closer meaning and a more natural English sentence.";
        }

        private static bool IsMeaningfulSuggestedRewrite(string suggestedRewrite)
        {
            if (string.IsNullOrWhiteSpace(suggestedRewrite))
            {
                return false;
            }

            if (!suggestedRewrite.Any(char.IsLetter))
            {
                return false;
            }

            var normalizedRewrite = suggestedRewrite.ToLowerInvariant();
            return !normalizedRewrite.StartsWith("try again", StringComparison.Ordinal)
                && !normalizedRewrite.Contains("teacher reference", StringComparison.Ordinal)
                && !normalizedRewrite.Contains("hidden reference", StringComparison.Ordinal)
                && !normalizedRewrite.Contains("learner answer", StringComparison.Ordinal)
                && !normalizedRewrite.Contains("closer meaning", StringComparison.Ordinal)
                && !normalizedRewrite.Contains("more natural english", StringComparison.Ordinal)
                && !normalizedRewrite.Contains("corrected sentence", StringComparison.Ordinal)
                && !normalizedRewrite.Contains("suggested rewrite", StringComparison.Ordinal);
        }

        private static WritingSentenceEvaluationSnapshotViewModel? BuildEvaluationSnapshot(WritingAttemptRow attempt)
        {
            if (string.IsNullOrWhiteSpace(attempt.SummaryTitle)
                && string.IsNullOrWhiteSpace(attempt.SummaryText)
                && string.IsNullOrWhiteSpace(attempt.ReviewText)
                && string.IsNullOrWhiteSpace(attempt.MeaningFeedback)
                && string.IsNullOrWhiteSpace(attempt.GrammarFeedback)
                && string.IsNullOrWhiteSpace(attempt.NaturalnessFeedback)
                && string.IsNullOrWhiteSpace(attempt.WordChoiceFeedback)
                && string.IsNullOrWhiteSpace(attempt.SuggestedRewrite))
            {
                return null;
            }

            return new WritingSentenceEvaluationSnapshotViewModel
            {
                Passed = attempt.Passed,
                UsedAi = attempt.UsedAi,
                EvaluationSource = attempt.EvaluationSource,
                SummaryTitle = attempt.SummaryTitle ?? string.Empty,
                SummaryText = attempt.SummaryText ?? string.Empty,
                ReviewText = attempt.ReviewText ?? string.Empty,
                MeaningFeedback = attempt.MeaningFeedback ?? string.Empty,
                GrammarFeedback = attempt.GrammarFeedback ?? string.Empty,
                NaturalnessFeedback = attempt.NaturalnessFeedback ?? string.Empty,
                WordChoiceFeedback = attempt.WordChoiceFeedback ?? string.Empty,
                SuggestedRewrite = attempt.SuggestedRewrite ?? string.Empty
            };
        }

        private static string? NormalizeSnapshotText(string? value, int maxLength, bool preserveLineBreaks = false)
        {
            var normalizedValue = preserveLineBreaks
                ? NormalizeMultilineSnapshotText(value)
                : CollapseWhitespace(value);

            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return null;
            }

            return normalizedValue.Length <= maxLength
                ? normalizedValue
                : normalizedValue[..maxLength];
        }

        private static string NormalizeMultilineSnapshotText(string? value)
        {
            var normalizedLines = (value ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(CollapseWhitespace)
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join("\n", normalizedLines);
        }

        private static string BuildGrammarFeedback(string userAnswer, bool fallbackPass)
        {
            var trimmedAnswer = CollapseWhitespace(userAnswer);
            var hasCapitalizedStart = trimmedAnswer.Length > 0 && (!char.IsLetter(trimmedAnswer[0]) || char.IsUpper(trimmedAnswer[0]));
            var hasTerminalPunctuation = trimmedAnswer.EndsWith(".", StringComparison.Ordinal)
                || trimmedAnswer.EndsWith("!", StringComparison.Ordinal)
                || trimmedAnswer.EndsWith("?", StringComparison.Ordinal);

            if (fallbackPass && hasCapitalizedStart && hasTerminalPunctuation)
            {
                return "Dấu hiệu đầu câu và cuối câu nhìn ổn ở bước kiểm tra nhanh.";
            }

            if (!hasCapitalizedStart && !hasTerminalPunctuation)
            {
                return "Hãy viết hoa chữ đầu câu và kết thúc câu bằng dấu câu.";
            }

            if (!hasCapitalizedStart)
            {
                return "Hãy viết hoa chữ đầu câu.";
            }

            if (!hasTerminalPunctuation)
            {
                return "Hãy thêm dấu câu cuối câu để câu hoàn chỉnh hơn.";
            }

            return fallbackPass
                ? "Cấu trúc câu tạm chấp nhận được trong bước kiểm tra nhanh."
                : "Hãy kiểm tra lại ngữ pháp và ranh giới câu trước khi gửi lại.";
        }

        private static string CollapseWhitespace(string? value)
        {
            return string.Join(
                ' ',
                (value ?? string.Empty)
                    .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string NormalizeForComparison(string? value)
        {
            var collapsed = CollapseWhitespace(value).ToLowerInvariant();
            var buffer = new StringBuilder(collapsed.Length);

            foreach (var character in collapsed)
            {
                buffer.Append(char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) ? character : ' ');
            }

            return CollapseWhitespace(buffer.ToString());
        }

        private static string NormalizeVietnameseDisplayText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("Tron trong", "Tran trong", StringComparison.Ordinal)
                .Replace("Chuc may mon", "Chuc may man", StringComparison.Ordinal);
        }

        private static List<string> ExtractComparisonTokens(string? value)
        {
            var normalized = NormalizeForComparison(value);
            return normalized.Length == 0
                ? new List<string>()
                : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static List<string> ExtractMeaningfulComparisonTokens(string? value)
        {
            return ExtractComparisonTokens(value)
                .Where(token => !CommonEnglishStopWords.Contains(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int CountSentenceLikeSegments(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var segmentCount = 0;
            var hasWordContent = false;

            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    hasWordContent = true;
                    continue;
                }

                if (!hasWordContent)
                {
                    continue;
                }

                if (character is '.' or '!' or '?' or '\r' or '\n')
                {
                    segmentCount++;
                    hasWordContent = false;
                }
            }

            if (hasWordContent)
            {
                segmentCount++;
            }

            return segmentCount;
        }

        private sealed record WritingExerciseCatalog(
            string SelectedLevelKey,
            string SelectedLevelTitle,
            string SelectedContentTypeKey,
            string SelectedContentTypeTitle,
            List<WritingExerciseCardViewModel> Exercises,
            List<WritingExerciseCardViewModel> MyExercises,
            bool HasProgressMetadata,
            bool IsMyExercisesLocked,
            bool CanCreateFromAi);

        private sealed class WritingAttemptRow
        {
            public int ExerciseId { get; set; }
            public int SentenceId { get; set; }
            public int SentenceNumber { get; set; }
            public string SubmittedAnswer { get; set; } = string.Empty;
            public bool Passed { get; set; }
            public bool UsedAi { get; set; }
            public string EvaluationSource { get; set; } = string.Empty;
            public string? SummaryTitle { get; set; }
            public string? SummaryText { get; set; }
            public string? ReviewText { get; set; }
            public string? MeaningFeedback { get; set; }
            public string? GrammarFeedback { get; set; }
            public string? NaturalnessFeedback { get; set; }
            public string? WordChoiceFeedback { get; set; }
            public string? SuggestedRewrite { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }

        private sealed class WritingExerciseProgressSummary
        {
            public int AttemptCount { get; set; }
            public int CompletedSentenceCount { get; set; }
            public string StatusKey { get; set; } = WritingStatusNotStarted;
            public string StatusLabel { get; set; } = "Chưa bắt đầu";
            public DateTime? LastAttemptedAtUtc { get; set; }
            public int? LatestAttemptSentenceId { get; set; }
            public Dictionary<int, int> SentenceAttemptCounts { get; } = new();
            public Dictionary<int, WritingSentenceProgressState> SentenceStates { get; } = new();
            public List<WritingAttemptRow> RecentAttempts { get; set; } = new();
        }

        private sealed class WritingSentenceProgressState
        {
            public string LastSubmittedAnswer { get; set; } = string.Empty;
            public string AcceptedAnswer { get; set; } = string.Empty;
            public bool HasAccepted { get; set; }
            public bool? LastEvaluationPassed { get; set; }
            public WritingSentenceEvaluationSnapshotViewModel? LastEvaluation { get; set; }
        }

        private sealed class WritingPracticeExerciseRow
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Level { get; set; } = string.Empty;
            public string ContentType { get; set; } = string.Empty;
            public string Topic { get; set; } = string.Empty;
            public string PreviewText { get; set; } = string.Empty;
        }

        private sealed class WritingSentenceLookupRow
        {
            public int Id { get; set; }
            public int SortOrder { get; set; }
            public string VietnameseText { get; set; } = string.Empty;
            public string EnglishMeaning { get; set; } = string.Empty;
        }

        private sealed class WritingSentenceContentOverlap
        {
            public int SentenceId { get; set; }
            public int SentenceNumber { get; set; }
            public int MatchedContentTokens { get; set; }
            public double Coverage { get; set; }
        }

        private sealed class WritingRuleEvaluationResult
        {
            public int ExerciseId { get; set; }
            public int SentenceId { get; set; }
            public int SentenceNumber { get; set; }
            public bool Passed { get; set; }
            public bool CanAutoAdvance { get; set; }
            public bool UsedAi { get; set; }
            public string EvaluationSource { get; set; } = string.Empty;
            public bool IsHardFailure { get; set; }
            public string SummaryTitle { get; set; } = string.Empty;
            public string SummaryText { get; set; } = string.Empty;
            public string MeaningFeedback { get; set; } = string.Empty;
            public string GrammarFeedback { get; set; } = string.Empty;
            public string NaturalnessFeedback { get; set; } = string.Empty;
            public string WordChoiceFeedback { get; set; } = string.Empty;
            public string SuggestedRewrite { get; set; } = string.Empty;
        }

    }
}

