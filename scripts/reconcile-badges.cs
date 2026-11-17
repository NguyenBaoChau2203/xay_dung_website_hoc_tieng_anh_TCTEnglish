// Run with .NET SDK 10: dotnet run --file scripts/reconcile-badges.cs -- <id,id,...> [--apply]
// Explicit local operation only. No web host, seeders, workers or network providers are started.
#:project ../TCTEnglish/TCTEnglish.csproj
#:package Microsoft.EntityFrameworkCore.Relational@9.0.4
#:property PublishAot=false
#:property PublishTrimmed=false

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using TCTEnglish.Services.Learning;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--apply"))
    throw new ArgumentException("Use <id,id,...> [--apply]; dry-run is the default. Maximum 100 explicit IDs.");
var ids = args[0].Split(',').Select(int.Parse).Distinct().ToArray();
if (ids.Length is < 1 or > 100 || ids.Any(id => id <= 0)) throw new ArgumentException("Supply 1–100 positive user IDs.");
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Set the local database child-process connection; never pass credentials as arguments.");
var parsed = new MySqlConnectionStringBuilder(connection);
if (parsed.Server is not ("127.0.0.1" or "localhost" or "::1")
    || !(parsed.Database == "tctenglish_dev" && parsed.Port == 3307
         || parsed.Database.StartsWith("tct_acceptance_", StringComparison.Ordinal) && parsed.Port is >= 3308 and <= 3399))
    throw new InvalidOperationException("Only the known Docker development DB or a loopback disposable acceptance DB is allowed.");
await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
    .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
if ((await db.Database.GetPendingMigrationsAsync()).Any())
    throw new InvalidOperationException("Apply reviewed local migrations first. This command never migrates or seeds.");
var clock = new LearningBusinessClock(TimeProvider.System);
var gate = new DirectNotificationGate();
var notifications = new NotificationService(db, clock, NullLogger<NotificationService>.Instance);
var streak = new StreakService(db, notifications, clock, gate, NullLogger<StreakService>.Instance);
var goals = new GoalsService(db, streak, notifications, gate, NullLogger<GoalsService>.Instance,
    clock, new LearningNotificationIntentWriter(db, clock));
var output = Path.Combine(Directory.GetCurrentDirectory(), ".appdata", "local", "badge-reconciliation");
Directory.CreateDirectory(output);
var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
var dry = await goals.ReconcileNewBadgesAsync(ids);
await File.WriteAllTextAsync(Path.Combine(output, $"{stamp}-dry-run.json"), JsonSerializer.Serialize(dry));
Console.WriteLine($"Dry-run: {dry.Count} requested users, {dry.Sum(row => row.BadgeIds.Count)} missing eligible awards. Receipt saved under .appdata/local/badge-reconciliation.");
if (args.Length == 2)
{
    // Re-evaluate under user locks; no grants from a stale preview, no XP/history writes or catch-up notifications.
    var applied = await goals.ReconcileNewBadgesAsync(ids, dryRun: false);
    await File.WriteAllTextAsync(Path.Combine(output, $"{stamp}-applied.json"), JsonSerializer.Serialize(applied));
    Console.WriteLine($"Applied: {applied.Sum(row => row.BadgeIds.Count)} retroactive awards; notification fan-out suppressed.");
}
