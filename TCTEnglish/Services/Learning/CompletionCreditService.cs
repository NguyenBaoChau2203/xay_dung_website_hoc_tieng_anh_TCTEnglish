using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Learning;

public sealed class CompletionCreditApplyResult
{
    public required bool Applied { get; init; }
    public int XpEarned { get; init; }
    public int Streak { get; init; }
    public OperationStatus Status { get; init; } = OperationStatus.Success;
    public string? ErrorMessage { get; init; }
    public bool Replay { get; init; }
}

public interface ICompletionCreditService
{
    Task<CompletionCreditApplyResult> TryApplySpeakingVideoCompletionAsync(
        int userId,
        int videoId,
        GoalsActivityUpdate activityUpdate,
        CancellationToken cancellationToken);

    Task<CompletionCreditApplyResult> TryApplyWritingExerciseCompletionAsync(
        int userId,
        int exerciseId,
        GoalsActivityUpdate activityUpdate,
        CancellationToken cancellationToken);
}

public sealed class CompletionCreditService : ICompletionCreditService
{
    private readonly DbflashcardContext _context;
    private readonly IGoalsService _goalsService;
    private readonly LearningBusinessClock _clock;
    private readonly DirectNotificationGate _gate;
    private readonly ILearningNotificationIntentWriter _intentWriter;
    private readonly ILogger<CompletionCreditService> _logger;

    public CompletionCreditService(
        DbflashcardContext context,
        IGoalsService goalsService,
        LearningBusinessClock clock,
        DirectNotificationGate gate,
        ILearningNotificationIntentWriter intentWriter,
        ILogger<CompletionCreditService> logger)
    {
        _context = context;
        _goalsService = goalsService;
        _clock = clock;
        _gate = gate;
        _intentWriter = intentWriter;
        _logger = logger;
    }

    public Task<CompletionCreditApplyResult> TryApplySpeakingVideoCompletionAsync(
        int userId,
        int videoId,
        GoalsActivityUpdate activityUpdate,
        CancellationToken cancellationToken)
        => ApplyAsync(userId, "Speaking", $"video:{videoId}", activityUpdate, cancellationToken);

    public Task<CompletionCreditApplyResult> TryApplyWritingExerciseCompletionAsync(
        int userId,
        int exerciseId,
        GoalsActivityUpdate activityUpdate,
        CancellationToken cancellationToken)
        => ApplyAsync(userId, "Writing", $"exercise:{exerciseId}", activityUpdate, cancellationToken);

    private async Task<CompletionCreditApplyResult> ApplyAsync(
        int userId,
        string feature,
        string contentKey,
        GoalsActivityUpdate activityUpdate,
        CancellationToken cancellationToken)
    {
        if (activityUpdate.HasNegativeValues)
        {
            return new CompletionCreditApplyResult
            {
                Applied = false,
                Status = OperationStatus.Invalid,
                ErrorMessage = "Invalid activity increments."
            };
        }

        var creditKey = $"{userId}:{feature}:{contentKey}";
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var exists = await _context.LearningCompletionCredits
                .AsNoTracking()
                .AnyAsync(c => c.CreditKey == creditKey, cancellationToken);
            if (exists)
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return new CompletionCreditApplyResult { Applied = false, Replay = true, XpEarned = 0, Streak = 0 };
            }

            var occurredAt = _clock.UtcNow;
            _context.LearningCompletionCredits.Add(new LearningCompletionCredit
            {
                UserId = userId,
                Feature = feature,
                ContentKey = contentKey,
                CreditKey = creditKey,
                XpAwarded = activityUpdate.XpEarned,
                BusinessDate = _clock.BusinessDate,
                CreatedAtUtc = occurredAt
            });

            _gate.SuppressDirect = true;
            GoalsActivityRecordResult rewardResult;
            try
            {
                rewardResult = await _goalsService.RecordLearningActivityAsync(userId, activityUpdate);
            }
            finally
            {
                _gate.SuppressDirect = false;
            }

            if (rewardResult.Status != OperationStatus.Success)
            {
                if (ownsTransaction && transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return new CompletionCreditApplyResult
                {
                    Applied = false,
                    Status = rewardResult.Status,
                    ErrorMessage = rewardResult.ErrorMessage
                };
            }

            await _intentWriter.EnqueueAsync(
                userId,
                feature,
                contentKey,
                occurredAt,
                rewardResult.Transition,
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            if (ownsTransaction && transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new CompletionCreditApplyResult
            {
                Applied = true,
                XpEarned = activityUpdate.XpEarned,
                Streak = rewardResult.Streak,
                Status = OperationStatus.Success
            };
        }
        catch (DbUpdateException ex) when (CreditKeyConflict.IsDuplicateCreditKey(ex))
        {
            _logger.LogInformation(ex, "Completion credit replay for {CreditKey}", creditKey);
            if (ownsTransaction && transaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                await ResetTrackerAfterRollbackAsync();
            }
            else
            {
                DetachAddedCredits();
            }

            return new CompletionCreditApplyResult { Applied = false, Replay = true, XpEarned = 0, Streak = 0 };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ownsTransaction && transaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            if (ownsTransaction)
            {
                await ResetTrackerAfterRollbackAsync();
            }

            throw;
        }
        finally
        {
            if (ownsTransaction)
            {
                await transaction!.DisposeAsync();
            }
        }
    }

    private void DetachAddedCredits()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Added
                && entry.Entity is LearningCompletionCredit or LearningNotificationOutbox)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private async Task ResetTrackerAfterRollbackAsync()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Added)
            {
                entry.State = EntityState.Detached;
            }
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                await entry.ReloadAsync();
            }
        }
    }
}

internal static class CreditKeyConflict
{
    public static bool IsDuplicateCreditKey(DbUpdateException exception)
    {
        var message = exception.ToString();
        var mentionsCredit = message.Contains("CreditKey", StringComparison.OrdinalIgnoreCase)
            || message.Contains("IX_LearningCompletionCredits_CreditKey", StringComparison.OrdinalIgnoreCase);

        if (!mentionsCredit)
        {
            return false;
        }

        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.GetType().Name == "MySqlException")
            {
                var number = current.GetType().GetProperty("Number")?.GetValue(current);
                return number is int mysql && mysql == 1062;
            }

            if (string.Equals(current.GetType().FullName, "Microsoft.Data.Sqlite.SqliteException", StringComparison.Ordinal))
            {
                var code = current.GetType().GetProperty("SqliteErrorCode")?.GetValue(current);
                return code is int sqlite && sqlite == 19;
            }
        }

        return false;
    }
}
