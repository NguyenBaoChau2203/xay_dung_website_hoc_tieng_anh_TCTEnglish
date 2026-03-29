using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.ViewModels.AI;

namespace TCTEnglish.Services.AI;

public interface IAiObservabilityService
{
    Task<AiObservabilitySnapshotDto> GetUserSnapshotAsync(int userId, int lookbackDays, CancellationToken ct);
}

