using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services
{
    public partial class StudyService
    {
        private static readonly SemaphoreSlim WritingSchemaInitializationLock = new(1, 1);
        private static readonly JsonSerializerOptions WritingJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        private static readonly HashSet<string> CommonEnglishStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "are", "as", "at", "be", "been", "being", "but", "by",
            "for", "from", "had", "has", "have", "he", "her", "hers", "him", "his",
            "i", "if", "in", "is", "it", "its", "me", "my", "of", "on", "or", "our",
            "ours", "she", "that", "the", "their", "theirs", "them", "there", "they",
            "this", "to", "us", "was", "we", "were", "will", "with", "you", "your", "yours"
        };
        private static volatile bool _writingSchemaInitialized;

        public Task<WritingIndexViewModel> GetWritingIndexViewModelAsync(string? selectedLevel)
        {
            return Task.FromResult(BuildWritingIndexViewModel(selectedLevel));
        }

        public async Task<WritingExerciseDataViewModel> GetWritingExerciseDataAsync(
            string? selectedLevel,
            string? contentType,
            string? topic)
        {
            var catalog = await GetWritingExerciseCatalogAsync(selectedLevel, contentType);
            return BuildWritingExerciseDataViewModel(catalog, topic);
        }

        public async Task<WritingExerciseListViewModel> GetWritingExerciseListViewModelAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            string? status,
            int page)
        {
            var exerciseData = await GetWritingExerciseDataAsync(selectedLevel, contentType, topic);
            return BuildWritingExerciseListViewModel(exerciseData, status, page);
        }

        public async Task<WritingPracticeDataViewModel?> GetWritingPracticeDataAsync(int exerciseId)
        {
            await EnsureWritingSchemaReadyAsync();

            var exercise = await _context.WritingExercises
                .AsNoTracking()
                .Where(item => item.IsPublished && item.Id == exerciseId)
                .Select(item => new WritingPracticeExerciseRow
                {
                    Id = item.Id,
                    Title = item.Title,
                    Level = item.Level,
                    ContentType = item.ContentType,
                    Topic = item.Topic,
                    PreviewText = item.PreviewText
                })
                .FirstOrDefaultAsync();

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

            return BuildWritingPracticeDataViewModel(exercise, sentences);
        }

        public async Task<WritingSentenceHintViewModel?> GetWritingSentenceHintAsync(int exerciseId, int sentenceId)
        {
            if (sentenceId <= 0)
            {
                return null;
            }

            await EnsureWritingSchemaReadyAsync();

            var sentences = await LoadPublishedWritingSentenceRowsAsync(exerciseId);
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
                HintTitle = BuildHintTitle(sentence.VietnameseText, sentenceIndex, sentences.Count),
                HintText = BuildHintText(sentence.VietnameseText, sentenceIndex, sentences.Count)
            };
        }

        public async Task<WritingSentenceEvaluationViewModel?> EvaluateWritingSentenceAsync(
            int exerciseId,
            int sentenceId,
            string userAnswer)
        {
            await EnsureWritingSchemaReadyAsync();

            var sentences = await LoadPublishedWritingSentenceRowsAsync(exerciseId);
            var sentenceIndex = sentences.FindIndex(sentence => sentence.Id == sentenceId);
            if (sentenceIndex < 0)
            {
                return null;
            }

            var sentence = sentences[sentenceIndex];
            var normalizedAnswer = CollapseWhitespace(userAnswer);
            var ruleEvaluation = EvaluateWritingSentenceRuleBased(
                exerciseId,
                sentence,
                sentenceIndex,
                sentences.Count,
                normalizedAnswer);

            if (ruleEvaluation.IsHardFailure)
            {
                return BuildWritingSentenceEvaluationViewModel(ruleEvaluation);
            }

            var aiEvaluation = await TryEvaluateWritingSentenceWithAiAsync(sentence, normalizedAnswer);
            if (aiEvaluation == null)
            {
                return BuildWritingSentenceEvaluationViewModel(ruleEvaluation);
            }

            var finalPassed = aiEvaluation.Passed;
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
                SuggestedRewrite = aiEvaluation.SuggestedRewrite
            };
        }

        public async Task<WritingPracticeViewModel?> GetWritingPracticeViewModelAsync(
            string? selectedLevel,
            string? contentType,
            string? topic,
            string? status,
            int page,
            int? exerciseId)
        {
            var catalog = await GetWritingExerciseCatalogAsync(selectedLevel, contentType);
            var exerciseData = BuildWritingExerciseDataViewModel(catalog, topic);
            var exerciseListViewModel = BuildWritingExerciseListViewModel(exerciseData, status, page);
            var selectedExerciseId = ResolveSelectedWritingExerciseId(exerciseId, exerciseListViewModel.Exercises, catalog.Exercises);

            if (!selectedExerciseId.HasValue)
            {
                return null;
            }

            var practiceData = await GetWritingPracticeDataAsync(selectedExerciseId.Value);
            if (practiceData == null)
            {
                return null;
            }

            var selectedPage = exerciseListViewModel.CurrentPage > 0
                ? exerciseListViewModel.CurrentPage
                : Math.Max(page, 1);

            return BuildWritingPracticeViewModel(exerciseListViewModel, practiceData, selectedPage);
        }

        private async Task<WritingExerciseCatalog> GetWritingExerciseCatalogAsync(string? selectedLevel, string? contentType)
        {
            var writingIndexViewModel = BuildWritingIndexViewModel(selectedLevel);
            var selectedContentTypeKey = ResolveWritingContentTypeKey(contentType);

            return new WritingExerciseCatalog(
                writingIndexViewModel.SelectedLevelKey,
                writingIndexViewModel.SelectedLevelTitle,
                selectedContentTypeKey,
                ResolveWritingContentTypeTitle(selectedContentTypeKey),
                await LoadWritingExerciseCardsAsync(writingIndexViewModel.SelectedLevelKey, selectedContentTypeKey));
        }

        private static WritingExerciseDataViewModel BuildWritingExerciseDataViewModel(
            WritingExerciseCatalog catalog,
            string? topic)
        {
            var normalizedTopic = NormalizeWritingTopic(topic);

            return new WritingExerciseDataViewModel
            {
                SelectedLevelKey = catalog.SelectedLevelKey,
                SelectedLevelTitle = catalog.SelectedLevelTitle,
                SelectedContentTypeKey = catalog.SelectedContentTypeKey,
                SelectedContentTypeTitle = catalog.SelectedContentTypeTitle,
                SelectedTopic = normalizedTopic,
                TopicOptions = BuildWritingTopicOptions(catalog.Exercises),
                Exercises = FilterWritingExercisesByTopic(catalog.Exercises, normalizedTopic)
            };
        }

        private static WritingExerciseListViewModel BuildWritingExerciseListViewModel(
            WritingExerciseDataViewModel exerciseData,
            string? status,
            int page)
        {
            const int pageSize = 6;

            var normalizedStatus = NormalizeWritingStatus(status);
            var filteredExercises = FilterWritingExercisesByStatus(exerciseData.Exercises, normalizedStatus);
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
                SelectedStatus = normalizedStatus,
                CurrentPage = currentPage,
                TotalPages = totalPages,
                TotalCount = totalCount,
                PageSize = pageSize,
                StartItemNumber = totalPages == 0 ? 0 : ((currentPage - 1) * pageSize) + 1,
                EndItemNumber = totalPages == 0 ? 0 : Math.Min(currentPage * pageSize, totalCount),
                TopicOptions = exerciseData.TopicOptions,
                StatusOptions = GetWritingStatusOptions(),
                PageNumbers = BuildWritingPageNumbers(currentPage, totalPages),
                Exercises = pagedItems
            };
        }

        private async Task<List<WritingExerciseCardViewModel>> LoadWritingExerciseCardsAsync(string levelKey, string contentTypeKey)
        {
            await EnsureWritingSchemaReadyAsync();

            return await _context.WritingExercises
                .AsNoTracking()
                .Where(exercise => exercise.IsPublished
                    && exercise.Level == levelKey
                    && exercise.ContentType == contentTypeKey)
                .OrderBy(exercise => exercise.Id)
                .Select(exercise => new WritingExerciseCardViewModel
                {
                    Id = exercise.Id,
                    Title = exercise.Title,
                    PreviewText = exercise.PreviewText,
                    Topic = exercise.Topic,
                    StatusKey = "new",
                    StatusLabel = "Mới",
                    AttemptCount = 0,
                    LastAttemptText = "Chưa bắt đầu"
                })
                .ToListAsync();
        }

        private async Task EnsureWritingSchemaReadyAsync()
        {
            if (_writingSchemaInitialized || !_context.Database.IsSqlServer())
            {
                return;
            }

            await WritingSchemaInitializationLock.WaitAsync();
            try
            {
                if (_writingSchemaInitialized)
                {
                    return;
                }

                await _context.Database.MigrateAsync();
                _writingSchemaInitialized = true;
            }
            finally
            {
                WritingSchemaInitializationLock.Release();
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
            string normalizedStatus)
        {
            return string.Equals(normalizedStatus, "all", StringComparison.OrdinalIgnoreCase)
                ? exercises.ToList()
                : exercises
                    .Where(exercise => string.Equals(exercise.StatusKey, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        private static int? ResolveSelectedWritingExerciseId(
            int? requestedExerciseId,
            IReadOnlyCollection<WritingExerciseCardViewModel> pagedExercises,
            IReadOnlyCollection<WritingExerciseCardViewModel> catalogExercises)
        {
            if (requestedExerciseId.HasValue && catalogExercises.Any(exercise => exercise.Id == requestedExerciseId.Value))
            {
                return requestedExerciseId.Value;
            }

            return pagedExercises.FirstOrDefault()?.Id ?? catalogExercises.FirstOrDefault()?.Id;
        }

        private static WritingPracticeDataViewModel BuildWritingPracticeDataViewModel(
            WritingPracticeExerciseRow exercise,
            IReadOnlyList<WritingExerciseSentence> sentences)
        {
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
                Sentences = BuildWritingPracticeSentences(sentences)
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
                Sentences = practiceData.Sentences
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

        private static List<WritingPracticeSentenceViewModel> BuildWritingPracticeSentences(IReadOnlyList<WritingExerciseSentence> sentences)
        {
            return sentences
                .Select((sentence, index) => new WritingPracticeSentenceViewModel
                {
                    Id = sentence.Id,
                    Number = index + 1,
                    VietnameseText = NormalizeVietnameseDisplayText(sentence.VietnameseText),
                    Placeholder = "Nhập câu tiếng Anh cho dòng đang chọn...",
                    BreakAfter = sentence.BreakAfter
                })
                .ToList();
        }

        private static string BuildLessonSummary(WritingExerciseCardViewModel selectedExercise, int sentenceCount)
        {
            return $"Hoàn thành bài viết về chủ đề {selectedExercise.Topic.ToLowerInvariant()} qua {sentenceCount} bước ngắn.";
        }

        private static List<string> BuildWritingTips(string contentTypeKey)
        {
            if (string.Equals(contentTypeKey, "emails", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    "Giữ lời chào, câu hỏi và câu kết ngắn gọn, tự nhiên.",
                    "Dịch từng dòng được đánh dấu và bám sát ý gốc.",
                    "Dùng tiếng Anh đơn giản, lịch sự và giống cách viết email thật."
                };
            }

            return new List<string>
            {
                "Hãy dịch rõ ràng dòng đang được đánh dấu trước khi chuyển tiếp.",
                "Giữ đúng ý và diễn đạt tự nhiên.",
                "Dựa vào câu tham chiếu để nhận ra giọng điệu và cấu trúc câu."
            };
        }

        private static List<WritingLessonSegmentViewModel> BuildLessonSegments(IReadOnlyList<WritingExerciseSentence> sentences)
        {
            var segments = new List<WritingLessonSegmentViewModel>();

            for (var index = 0; index < sentences.Count; index++)
            {
                var sentence = sentences[index];

                segments.Add(new WritingLessonSegmentViewModel
                {
                    Text = sentence.VietnameseText,
                    HighlightKey = BuildHighlightKey(sentence.SortOrder)
                });

                if (sentence.BreakAfter && index < sentences.Count - 1)
                {
                    segments.Add(new WritingLessonSegmentViewModel
                    {
                        Text = string.Empty
                    });
                }
            }

            return segments;
        }

        private static List<WritingPracticeStepViewModel> BuildWritingPracticeSteps(IReadOnlyList<WritingExerciseSentence> sentences)
        {
            return sentences
                .Select((sentence, index) => new WritingPracticeStepViewModel
                {
                    Number = index + 1,
                    PromptLabel = BuildPromptLabel(sentence.VietnameseText, index, sentences.Count),
                    PromptText = sentence.VietnameseText,
                    Placeholder = "Nhập câu tiếng Anh ở đây...",
                    HintTitle = BuildHintTitle(sentence.VietnameseText, index, sentences.Count),
                    HintText = BuildHintText(sentence.VietnameseText, index, sentences.Count),
                    HighlightKey = BuildHighlightKey(sentence.SortOrder),
                    SuccessTitle = "Đã khớp câu tham chiếu",
                    SuccessMessage = $"Câu tham chiếu: {sentence.EnglishMeaning}",
                    ErrorTitle = "Hãy xem lại câu tham chiếu",
                    ErrorMessage = $"Câu tham chiếu: {sentence.EnglishMeaning}",
                    AcceptedAnswers = new List<string>
                    {
                        sentence.EnglishMeaning
                    }
                })
                .ToList();
        }

        private static string BuildPromptLabel(string vietnameseText, int index, int totalCount)
        {
            if (IsSenderNameLine(index, totalCount))
            {
                return "Tên người gửi";
            }

            if (IsGreetingLine(index))
            {
                return "Lời chào";
            }

            if (IsClosingLine(vietnameseText, index, totalCount))
            {
                return "Lời kết";
            }

            if (vietnameseText.Contains('?'))
            {
                return "Câu hỏi";
            }

            return $"Câu {index + 1}";
        }

        private static string BuildHintTitle(string vietnameseText, int index, int totalCount)
        {
            if (IsSenderNameLine(index, totalCount))
            {
                return "Gợi ý tên";
            }

            if (IsGreetingLine(index))
            {
                return "Gợi ý lời chào";
            }

            if (IsClosingLine(vietnameseText, index, totalCount))
            {
                return "Gợi ý lời kết";
            }

            if (vietnameseText.Contains('?'))
            {
                return "Gợi ý câu hỏi";
            }

            return "Gợi ý dịch";
        }

        private static string BuildHintText(string vietnameseText, int index, int totalCount)
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
            var hasClosingPhrase = normalizedVietnameseText.Contains("Trân trọng", StringComparison.OrdinalIgnoreCase)
                || normalizedVietnameseText.Contains("Chúc may mắn", StringComparison.OrdinalIgnoreCase);

            return !IsGreetingLine(index)
                && !IsSenderNameLine(index, totalCount)
                && (index == totalCount - 2
                    || hasClosingPhrase);
        }

        private static string BuildHighlightKey(int sortOrder)
        {
            return $"sentence-{sortOrder}";
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

        private static List<WritingFilterOptionViewModel> GetWritingStatusOptions()
        {
            return new List<WritingFilterOptionViewModel>
            {
                new()
                {
                    Value = "all",
                    Label = "Tất cả trạng thái"
                },
                new()
                {
                    Value = "new",
                    Label = "Mới"
                },
                new()
                {
                    Value = "in-progress",
                    Label = "Đang làm"
                },
                new()
                {
                    Value = "completed",
                    Label = "Hoàn thành"
                }
            };
        }

        private static string NormalizeWritingTopic(string? topic)
        {
            return string.IsNullOrWhiteSpace(topic) ? "all" : topic.Trim();
        }

        private static string NormalizeWritingStatus(string? status)
        {
            var normalizedStatus = string.IsNullOrWhiteSpace(status)
                ? "all"
                : status.Trim().ToLowerInvariant();

            return normalizedStatus is "new" or "in-progress" or "completed"
                ? normalizedStatus
                : "all";
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

        private async Task<List<WritingSentenceLookupRow>> LoadPublishedWritingSentenceRowsAsync(int exerciseId)
        {
            var sentenceRows = await _context.WritingExerciseSentences
                .AsNoTracking()
                .Where(sentence => sentence.WritingExerciseId == exerciseId
                    && sentence.WritingExercise.IsPublished)
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

        private async Task<WritingAiEvaluationResult?> TryEvaluateWritingSentenceWithAiAsync(
            WritingSentenceLookupRow sentence,
            string userAnswer)
        {
            var apiKey = GetConfiguredOpenAiApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var model = GetWritingEvaluationModel();
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestPayload = new
            {
                model,
                temperature = 0.2,
                response_format = new
                {
                    type = "json_object"
                },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You evaluate ESL writing submissions. Use the reference answer only as a guide, not as the only acceptable wording. Accept meaning-equivalent paraphrases. Return strict JSON with keys: passed, overallFeedback, meaningFeedback, grammarFeedback, naturalnessFeedback, wordChoiceFeedback, suggestedRewrite. Write overallFeedback, meaningFeedback, grammarFeedback, naturalnessFeedback, and wordChoiceFeedback in Vietnamese for the learner. Keep suggestedRewrite as one improved English sentence."
                    },
                    new
                    {
                        role = "user",
                        content = BuildWritingEvaluationPrompt(sentence.VietnameseText, sentence.EnglishMeaning, userAnswer)
                    }
                }
            };

            using var response = await client.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Writing evaluation AI request failed for sentence {SentenceId} with status {StatusCode}: {ErrorBody}",
                    sentence.Id,
                    response.StatusCode,
                    errorBody);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var aiContent = ExtractAiResponseContent(responseBody);

            if (string.IsNullOrWhiteSpace(aiContent))
            {
                _logger.LogWarning("Writing evaluation AI response was empty for sentence {SentenceId}.", sentence.Id);
                return null;
            }

            try
            {
                var normalizedAiContent = NormalizeAiJsonPayload(aiContent);
                return JsonSerializer.Deserialize<WritingAiEvaluationResult>(normalizedAiContent, WritingJsonOptions);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Writing evaluation AI response was not valid JSON for sentence {SentenceId}.", sentence.Id);
                return null;
            }
        }

        private string? GetConfiguredOpenAiApiKey()
        {
            var apiKey = _configuration["OpenAiApiKey"] ?? _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)
                || apiKey.Contains("your_openai_api_key_here", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return apiKey;
        }

        private static string NormalizeAiJsonPayload(string aiContent)
        {
            var trimmed = aiContent.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                return trimmed;
            }

            var firstNewLineIndex = trimmed.IndexOf('\n');
            if (firstNewLineIndex < 0)
            {
                return trimmed;
            }

            var contentBody = trimmed[(firstNewLineIndex + 1)..].Trim();
            if (contentBody.EndsWith("```", StringComparison.Ordinal))
            {
                contentBody = contentBody[..^3].Trim();
            }

            return contentBody;
        }

        private string GetWritingEvaluationModel()
        {
            return _configuration["OpenAI:WritingEvaluationModel"]
                ?? _configuration["OpenAI:Model"]
                ?? _configuration["OpenAiModel"]
                ?? "gpt-4o-mini";
        }

        private static string BuildWritingEvaluationPrompt(
            string vietnameseText,
            string englishMeaning,
            string userAnswer)
        {
            return $"""
                Evaluate this learner translation.

                Vietnamese text:
                {vietnameseText}

                Teacher reference:
                {englishMeaning}

                Learner answer:
                {userAnswer}

                Requirements:
                - Judge the learner by meaning first, then grammar, naturalness, and word choice.
                - Accept meaning-equivalent paraphrases. Do not require an exact match to the teacher reference.
                - Set "passed" to true only when the learner answer is acceptable for moving to the next sentence.
                - Keep each feedback field short and actionable.
                - Write all feedback fields in Vietnamese so the learner can understand them quickly.
                - If the answer should be improved, suggest one better English sentence in "suggestedRewrite".
                - If the answer is already good, "suggestedRewrite" can be empty.
                """;
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

            var sentenceTypeHint = BuildHintText(sentence.VietnameseText, sentenceIndex, totalSentenceCount);
            var grammarFeedback = BuildGrammarFeedback(userAnswer, fallbackPass);
            var naturalnessFeedback = fallbackPass
                ? "Cấu trúc câu này tạm ổn và đủ rõ để bạn đi tiếp."
                : "Hãy bám sát ý gốc hơn và diễn đạt thành một câu tiếng Anh tự nhiên.";
            var wordChoiceFeedback = matchedContentTokens > 0
                ? "Bạn đã dùng được một vài từ khóa gần với ý cần diễn đạt, đó là khởi đầu tốt."
                : "Hãy thử đưa vào động từ chính hoặc danh từ quan trọng từ câu tiếng Việt.";

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
            WritingRuleEvaluationResult evaluation)
        {
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
                SuggestedRewrite = evaluation.SuggestedRewrite
            };
        }

        private static string? ExtractAiResponseContent(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return null;
            }

            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message)
                || !message.TryGetProperty("content", out var content))
            {
                return null;
            }

            if (content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }

            if (content.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type)
                    && string.Equals(type.GetString(), "text", StringComparison.OrdinalIgnoreCase)
                    && part.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }

            return null;
        }

        private static string BuildReviewText(
            string overallFeedback,
            string meaningFeedback,
            string grammarFeedback,
            string naturalnessFeedback,
            string wordChoiceFeedback)
        {
            return $"Tổng quan: {overallFeedback} Ý nghĩa: {meaningFeedback} Ngữ pháp: {grammarFeedback} Độ tự nhiên: {naturalnessFeedback} Từ vựng: {wordChoiceFeedback}";
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
                .Replace("TrÃ¢n trá»ng", "Trân trọng", StringComparison.Ordinal)
                .Replace("ChÃºc may máº¯n", "Chúc may mắn", StringComparison.Ordinal);
        }

        private static List<string> ExtractComparisonTokens(string? value)
        {
            var normalized = NormalizeForComparison(value);
            return normalized.Length == 0
                ? new List<string>()
                : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private sealed record WritingExerciseCatalog(
            string SelectedLevelKey,
            string SelectedLevelTitle,
            string SelectedContentTypeKey,
            string SelectedContentTypeTitle,
            List<WritingExerciseCardViewModel> Exercises);

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

        private sealed class WritingAiEvaluationResult
        {
            public bool Passed { get; set; }
            public string OverallFeedback { get; set; } = string.Empty;
            public string MeaningFeedback { get; set; } = string.Empty;
            public string GrammarFeedback { get; set; } = string.Empty;
            public string NaturalnessFeedback { get; set; } = string.Empty;
            public string WordChoiceFeedback { get; set; } = string.Empty;
            public string SuggestedRewrite { get; set; } = string.Empty;
        }
    }
}
