using System.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
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

        for (var retry = 0; ; retry++)
        {
            try
            {
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
            catch (Exception ex) when (retry < 2 && IsDeadlock(ex))
            {
                // Different users can contend on unique-index gaps even after locking their user rows.
                // The owned transaction has been disposed: retry the complete unit, never its last INSERT.
                context.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (retry + 1)), cancellationToken);
            }
        }
    }

    private static bool IsDeadlock(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is MySqlException { Number: 1213 }) return true;
        return false;
    }

    internal static async Task LockAsync(DbflashcardContext context, int userId, CancellationToken cancellationToken)
    {
        if (context.Database.IsMySql())
            await context.Database.SqlQuery<int>(
                $"SELECT `UserID` AS `Value` FROM `Users` WHERE `UserID` = {userId} FOR UPDATE")
                .ToListAsync(cancellationToken);
        else
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Users SET SecurityVersion = SecurityVersion WHERE UserID = {userId}", cancellationToken);
        var tracked = context.ChangeTracker.Entries<User>().FirstOrDefault(entry => entry.Entity.UserId == userId);
        if (tracked != null) await tracked.ReloadAsync(cancellationToken);
    }
}
