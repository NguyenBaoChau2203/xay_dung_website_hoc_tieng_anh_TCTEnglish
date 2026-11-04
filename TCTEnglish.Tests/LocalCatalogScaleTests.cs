using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class LocalCatalogScaleTests
{
    [CatalogScaleTheory]
    [InlineData(200)]
    [InlineData(2000)]
    public async Task MeasureCatalogsAndHistory(int size)
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        await using var factory = new MariaDbAcceptanceFactory(connection, Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID"));
        var counter = new RowCountingCommandInterceptor();
        factory.SaveInterceptors.Add(counter);
        await factory.InitializeAsync();
        var conversations = new Dictionary<int, Guid>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercises = Enumerable.Range(0, size).Select(i => new WritingExercise
            {
                Title = "Scale " + i, Level = "beginner", ContentType = "articles", Topic = "scale", SourceType = "manual",
                PreviewText = "Synthetic catalog sample", IsPublished = true, CreatedAt = DateTime.UtcNow,
                WritingExerciseSentences = [new() { SortOrder = 1, VietnameseText = "Tôi học.", EnglishMeaning = "I study." }]
            }).ToList();
            db.WritingExercises.AddRange(exercises);
            for (var i = 0; i < size; i++) db.SpeakingVideos.Add(new SpeakingVideo
            { Title = "Scale " + i, YoutubeId = "synthetic01", Level = "C2", Topic = "scale", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            foreach (var exercise in exercises)
                for (int i = 0; i < 3; i++) db.UserWritingAttempts.Add(new UserWritingAttempt
                { UserId = TestDataIds.UserId, WritingExerciseId = exercise.Id, WritingExerciseSentenceId = exercise.WritingExerciseSentences.Single().Id,
                    SubmittedAnswer = "I study.", Passed = i == 0, EvaluationSource = "offline", ReviewText = new string('x', 1000), CreatedAtUtc = DateTime.UtcNow });
            foreach (var count in new[] { 50, 500, 5000 })
            {
                var id = Guid.NewGuid(); conversations[count] = id;
                db.AiConversations.Add(new() { Id = id, UserId = TestDataIds.UserId, Title = "Scale history", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
                for (int i = 0; i < count; i++) db.AiMessages.Add(new() { Id = Guid.NewGuid(), ConversationId = id, Role = i % 2 == 0 ? TCTEnglish.Models.AiMessageRole.User : TCTEnglish.Models.AiMessageRole.Assistant,
                    Content = "Synthetic history " + i, CreatedAtUtc = DateTime.UtcNow.AddSeconds(i) });
            }
            await db.SaveChangesAsync();
        }
        var results = new List<object>();
        foreach (var scenario in new[] { "writing", "speaking", "history50", "history500", "history5000" })
        {
            var elapsed = new List<double>(); var allocations = new List<long>(); var rows = new List<long>(); var commands = new List<int>(); int bytes = 0;
            for (int sample = 0; sample < 35; sample++)
            {
                using var scope = factory.Services.CreateScope();
                counter.Reset();
                var before = GC.GetTotalAllocatedBytes(); var timer = Stopwatch.StartNew();
                object vm;
                if (scenario == "writing") vm = await scope.ServiceProvider.GetRequiredService<IWritingService>().GetWritingExerciseListViewModelAsync("beginner", "articles", "all", 1, TestDataIds.UserId, "all");
                else if (scenario == "speaking") vm = await new SpeakingService(scope.ServiceProvider.GetRequiredService<DbflashcardContext>(), scope.ServiceProvider.GetRequiredService<IYoutubeTranscriptService>(), Microsoft.Extensions.Logging.Abstractions.NullLogger<SpeakingService>.Instance)
                    .GetSpeakingIndexViewModelAsync(TestDataIds.UserId, new() { Level = "C2" });
                else vm = await scope.ServiceProvider.GetRequiredService<IAiConversationService>().GetMessagesPageAsync(TestDataIds.UserId, conversations[int.Parse(scenario[7..])], 50, null, null, default);
                timer.Stop();
                var allocated = GC.GetTotalAllocatedBytes() - before;
                if (sample >= 5) { elapsed.Add(timer.Elapsed.TotalMilliseconds); allocations.Add(allocated); rows.Add(counter.Rows); commands.Add(counter.Commands); }
                bytes = JsonSerializer.SerializeToUtf8Bytes(vm).Length;
            }
            elapsed.Sort();
            results.Add(new { scenario, size, samples = 30, warmup = 5, p50Ms = elapsed[15], p95Ms = elapsed[28], allocatedBytes = allocations.Average(), rows = rows.Average(), commands = commands.Average(), serializedViewModelBytes = bytes });
        }
        var prefix = Environment.GetEnvironmentVariable("TCT_PERF_REPORT_PREFIX")!;
        Assert.Contains(Path.DirectorySeparatorChar + ".tmp" + Path.DirectorySeparatorChar, Path.GetFullPath(prefix));
        await File.WriteAllTextAsync(prefix + "-" + size + ".json", JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class CatalogScaleTheoryAttribute : TheoryAttribute
{
    public CatalogScaleTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_PERF_REPORT_PREFIX")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")))
            Skip = "Opt-in synthetic catalog measurement on guarded disposable MariaDB.";
    }
}
