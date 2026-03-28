using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services
{
    public interface IGoalsService
    {
        Task<GoalsViewModel?> GetGoalsAsync(int userId);
        Task<OperationResult> UpdateGoalAsync(int userId, int dailyGoal);
        Task<OperationResult> RecordActivityAsync(int userId, GoalsActivityUpdate update);
    }
}
