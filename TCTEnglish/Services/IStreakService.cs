namespace TCTVocabulary.Services
{
    public interface IStreakService
    {
        Task<int> UpdateStreakAsync(int userId);
    }
}
