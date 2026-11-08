using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

internal static class AiLearnerAccess
{
    public static async Task<bool> CanUseOwnPrivateMediaAsync(
        DbflashcardContext context,
        int userId,
        CancellationToken ct)
    {
        var role = await context.Users
            .AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(user => user.Role)
            .FirstOrDefaultAsync(ct);

        var normalized = Roles.Normalize(role);
        return normalized is Roles.Admin or Roles.Premium;
    }
}
