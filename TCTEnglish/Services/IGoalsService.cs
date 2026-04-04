using TCTVocabulary.Models;
using TCTEnglish.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IGoalsService
    {
        Task<GoalsViewModel?> GetGoalsAsync(int userId);
        Task<OperationResult> UpdateGoalAsync(int userId, GoalArea goalArea, int targetValue);
        Task<OperationResult> RecordActivityAsync(int userId, GoalsActivityUpdate update);
        Task<GoalsActivityRecordResult> RecordLearningActivityAsync(int userId, GoalsActivityUpdate update);
        Task<StreakUpdateResult> UpdateStreakAndRewardsAsync(int userId);
        GoalsActivityUpdate BuildVocabularyActivityUpdate(bool isNewProgress, string? previousStatus, string currentStatus);
        GoalsActivityUpdate BuildSpeakingCompletionActivityUpdate();
        GoalsActivityUpdate BuildWritingCompletionActivityUpdate();
    }
}
