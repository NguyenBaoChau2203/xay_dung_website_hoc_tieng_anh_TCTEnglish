using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services
{
    public partial class StudyService : IStudyService
    {
        private readonly DbflashcardContext _context;

        public StudyService(DbflashcardContext context)
        {
            _context = context;
        }

        public async Task<StudyViewModel?> GetStudyViewModelAsync(int setId, int? userId = null)
        {
            var viewModel = await _context.Sets
                .AsNoTracking()
                .Where(s => s.SetId == setId)
                .Select(s => new StudyViewModel
                {
                    Set = new VocabularySetSummaryViewModel
                    {
                        SetId = s.SetId,
                        SetName = s.SetName,
                        FolderId = s.FolderId,
                        FolderName = s.Folder != null ? s.Folder.FolderName : null,
                        Description = s.Description
                    },
                    Cards = s.Cards
                        .Select(card => new VocabularyCardItemViewModel
                        {
                            CardId = card.CardId,
                            Term = card.Term,
                            Definition = card.Definition,
                            ImageUrl = card.ImageUrl,
                            Phonetic = card.Phonetic,
                            Example = card.Example,
                            ExampleTranslation = card.ExampleTranslation,
                            Topic = string.IsNullOrWhiteSpace(card.Topic) ? "Chưa phân loại" : card.Topic
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (viewModel == null)
            {
                return null;
            }

            viewModel.StudyTotal = viewModel.Cards.Count;

            if (!userId.HasValue || viewModel.Cards.Count == 0)
            {
                return viewModel;
            }

            var cardIds = viewModel.Cards
                .Select(card => card.CardId)
                .ToList();

            var progresses = await _context.LearningProgresses
                .AsNoTracking()
                .Where(lp => lp.UserId == userId.Value && cardIds.Contains(lp.CardId))
                .Select(lp => new
                {
                    lp.CardId,
                    lp.Status
                })
                .ToListAsync();

            viewModel.MasteredCardIds = progresses
                .Where(lp => lp.Status == "Mastered")
                .Select(lp => lp.CardId)
                .ToList();

            viewModel.LearningCardIds = progresses
                .Where(lp => lp.Status == "Learning" || lp.Status == "Reviewing")
                .Select(lp => lp.CardId)
                .ToList();

            return viewModel;
        }

        public async Task<OperationResult> UpdateCardProgressAsync(int cardId, bool isKnown, int userId)
        {
            var progress = await _context.LearningProgresses
                .FirstOrDefaultAsync(lp => lp.UserId == userId && lp.CardId == cardId);

            if (progress == null)
            {
                progress = new LearningProgress
                {
                    UserId = userId,
                    CardId = cardId,
                    Status = "Learning",
                    WrongCount = 0,
                    RepetitionCount = 0
                };

                _context.LearningProgresses.Add(progress);
            }

            progress.LastReviewedDate = DateTime.UtcNow;

            if (isKnown)
            {
                progress.Status = "Mastered";
                progress.RepetitionCount += 1;
                progress.NextReviewDate = DateTime.UtcNow.AddDays(7);
            }
            else
            {
                progress.Status = "Learning";
                progress.RepetitionCount = 0;
                progress.WrongCount = (progress.WrongCount ?? 0) + 1;
                progress.NextReviewDate = DateTime.UtcNow.AddMinutes(1);
            }

            await _context.SaveChangesAsync();
            return OperationResult.Success();
        }
    }
}
