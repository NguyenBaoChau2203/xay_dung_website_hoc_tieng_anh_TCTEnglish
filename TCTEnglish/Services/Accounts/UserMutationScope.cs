using System.Data;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Accounts;

/// <summary>Serializes account-scoped mutations across processes using the existing user row.</summary>
internal static class UserMutationScope
{
    public static async Task<T> ExecuteAsync<T>(DbflashcardContext context, int userId,
        Func<Task<T>> action, Func<T, bool> shouldCommit, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction != null)
        {
            await LockAsync(context, userId, cancellationToken);
            return await action();
        }

        var attempt = 0;
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            await LockAsync(context, userId, cancellationToken);
            var result = await action();
            if (shouldCommit(result)) await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static async Task LockAsync(DbflashcardContext context, int userId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Users SET SecurityVersion = SecurityVersion WHERE UserID = {userId}", cancellationToken);
        var tracked = context.ChangeTracker.Entries<User>().FirstOrDefault(entry => entry.Entity.UserId == userId);
        if (tracked != null) await tracked.ReloadAsync(cancellationToken);
    }
}
