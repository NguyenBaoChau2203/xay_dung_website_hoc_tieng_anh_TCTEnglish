using TCTEnglish.ViewModels;

namespace TCTEnglish.Services.Reading;

public interface IReadingAssistService
{
    Task<ReadingAssistResultVm> AssistAsync(int userId, ReadingAssistRequest request, CancellationToken ct = default);
}
