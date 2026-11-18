using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.Writing.Generation;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingBriefLifecycleTests
{
    internal static WritingCreateFromAiRequestViewModel Request(string key = "operation-1") => new()
    { Mode = "from_brief", Brief = "Write a polite leave request to visit family, with an unsupported flight detail.",
        Level = "beginner", ContentType = "emails", Length = "short", Tone = "formal", IdempotencyKey = key };
    internal static WritingTemplateConfirmation Choice() => new()
    { TemplateKey = "leave-request", TemplateVersion = 1, AcceptLimitedCoverage = true,
        Slots = new() { ["recipient"] = "An", ["startDate"] = "2026-12-01", ["endDate"] = "2026-12-03", ["reason"] = "family" } };
    internal static AiProviderReply Reply(string level = "beginner", string type = "emails", string tone = "formal", string length = "short")
    {
        var lines = WritingTemplateCatalog.Compose("leave-request", 1, "beginner", "formal", "short", Choice().Slots).Sentences;
        return new(JsonSerializer.Serialize(new { suggestedTitle = "An original leave request", suggestedTopic = "Work", suggestedLevel = level,
            suggestedContentType = type, tone, length, previewText = "A fictional leave request", sentences = lines.Select(s => new
            { vietnameseText = s.VietnameseText, englishMeaning = s.EnglishMeaning, breakAfter = s.BreakAfter }) }), 100, 200, 300, "fake-openai", "synthetic");
    }
    internal static async Task<WritingCreateFromAiResultViewModel> Create(TestWebApplicationFactory f, WritingCreateFromAiRequestViewModel request)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IWritingService>().CreateFromAiAsync(request, TestDataIds.UserId);
    }

    [Fact]
    public async Task All21ProfilesHaveOriginalOrderedAlignedBlocksAndAdvertiseOnlyActualCapabilities()
    {
        Assert.Equal(21, WritingTemplateCatalog.Capabilities.Count);
        Assert.Equal(6, WritingTemplateCatalog.Capabilities.Select(p => p.ContentType).Distinct().Count());
        foreach (var p in WritingTemplateCatalog.Capabilities)
        {
            var result = WritingTemplateCatalog.Compose(p.Key, p.Version, p.Level, p.Tones.Single(), p.Lengths.Single(),
                p.Key == "leave-request" ? Choice().Slots : new Dictionary<string, string>());
            Assert.Equal(7, result.Sentences.Count);
            Assert.Equal(7, result.Sentences.Select(s => s.EnglishMeaning).Distinct().Count());
            Assert.All(result.Sentences, s => { Assert.True(s.VietnameseText.Length > 10); Assert.True(s.EnglishMeaning.Length > 5);
                Assert.DoesNotContain("{", s.EnglishMeaning); Assert.DoesNotContain("|", s.VietnameseText); });
            Assert.True(result.Sentences.Last().BreakAfter);
            Assert.Throws<ArgumentException>(() => WritingTemplateCatalog.Compose(p.Key, p.Version, p.Level, p.Tones.Single(), "long", new Dictionary<string, string>()));
        }
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData("reason", "vì chuyến bay bị hủy")]
    [InlineData("recipient", "<script>alert(1)</script>")]
    [InlineData("endDate", "2026-11-01")]
    [InlineData("startDate", "tomorrow")]
    public void ArbitraryReasonsMarkupAndInvalidDatesAreNeverFakeTranslated(string field, string value)
    {
        var c = Choice(); c.Slots[field] = value;
        Assert.Throws<ArgumentException>(() => WritingTemplateCatalog.Compose("leave-request", 1, "advanced", "formal", "short", c.Slots));
    }

    [Fact]
    public async Task TemplateConfirmationReplayRestartPrivatePracticeAndRewards()
    {
        var calls = 0;
        await using var f = WritingBriefGenerationTests.Factory((_, _) => { calls++; throw new AiProviderException("synthetic", "network", true).WithRecordedUsage(5, 5, 10, 1000); });
        await WritingBriefGenerationTests.Premium(f);
        await AssertTemplateLifecycle(f, () => calls);
    }

    internal static async Task AssertTemplateLifecycle(TestWebApplicationFactory f, Func<int> calls)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var before = await WritingRecentExercisesTests.RewardSnapshot(db);
        var visits = await db.UserWritingExerciseVisits.CountAsync();
        var op = await Create(f, Request());
        Assert.Equal(WritingCreateFromAiOutcome.TemplateOffer, op.Outcome);
        Assert.True(op.ProviderOutcomeUncertain);
        Assert.Equal(10, op.KnownProviderTokens);
        Assert.Equal(1000, op.UncertainProviderTokenUpper);
        Assert.Equal("NeedsTemplateConfirmation", op.Coverage);
        Assert.Single(op.Templates);
        using var independent = f.Services.CreateScope();
        var service = independent.ServiceProvider.GetRequiredService<IWritingService>();
        var result = await service.ConfirmWritingTemplateAsync(TestDataIds.UserId, op.OperationId!.Value, Choice());
        Assert.Equal(WritingCreateFromAiOutcome.Success, result.Outcome);
        Assert.Equal("internal_template", result.GenerationMethod);
        Assert.Equal(result.ExerciseId, (await Create(f, Request())).ExerciseId);
        var replay = await service.ConfirmWritingTemplateAsync(TestDataIds.UserId, op.OperationId.Value, Choice());
        Assert.True(replay.IsReplay); Assert.Equal(result.ExerciseId, replay.ExerciseId);
        var changed = Choice(); changed.Slots["recipient"] = "Binh";
        Assert.Equal(WritingCreateFromAiOutcome.Conflict, (await service.ConfirmWritingTemplateAsync(TestDataIds.UserId, op.OperationId.Value, changed)).Outcome);
        Assert.Equal(1, calls());
        Assert.Equal(before, await WritingRecentExercisesTests.RewardSnapshot(db));
        Assert.Equal(visits, await db.UserWritingExerciseVisits.CountAsync());
        Assert.Equal(1, await db.WritingGenerationLogs.CountAsync(l => l.IsSuccess));
        var exercise = await db.WritingExercises.AsNoTracking().SingleAsync(e => e.Id == result.ExerciseId);
        Assert.False(exercise.IsPublished); Assert.Equal(TestDataIds.UserId, exercise.UserId);
        Assert.Equal("internal_template", exercise.GenerationMethod);
        var practice = await service.GetWritingPracticeDataAsync(result.ExerciseId, TestDataIds.UserId);
        Assert.NotNull(practice); Assert.Equal("internal_template", practice.Origin!.GenerationMethod);
        Assert.Null(await service.GetWritingPracticeDataAsync(result.ExerciseId, TestDataIds.OutsiderUserId));
        Assert.Null(await service.GetWritingGenerationAsync(TestDataIds.OutsiderUserId, op.OperationId.Value));
        Assert.Equal(Enumerable.Range(1, 7), practice.Sentences.Select(s => s.Number));
        var sentence = await db.WritingExerciseSentences.AsNoTracking().OrderBy(s => s.SortOrder).FirstAsync(s => s.WritingExerciseId == result.ExerciseId);
        var assessed = await service.EvaluateWritingSentenceAsync(result.ExerciseId, sentence.Id, sentence.EnglishMeaning, TestDataIds.UserId);
        Assert.NotNull(assessed); Assert.True(assessed.Passed);
    }

    [Fact]
    public async Task ValidBriefAuthorsFullContentAndStrictMetadataSurvivesPrivateRecovery()
    {
        IReadOnlyList<AiContextMessage>? captured = null;
        await using var f = WritingBriefGenerationTests.Factory((m, _) => { captured = m; return Task.FromResult(Reply()); });
        await WritingBriefGenerationTests.Premium(f);
        var result = await Create(f, Request());
        Assert.Equal(WritingCreateFromAiOutcome.Success, result.Outcome);
        Assert.Equal("openai", result.GenerationMethod); Assert.Equal(7, result.SentenceCount);
        Assert.Equal(300, result.KnownProviderTokens); Assert.False(result.ProviderOutcomeUncertain);
        Assert.Contains(captured!, m => m.Role == "user" && m.Content.Contains("minSentences", StringComparison.Ordinal));
        using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        Assert.Equal(result.ExerciseId, (await service.GetWritingGenerationAsync(TestDataIds.UserId, result.OperationId!.Value))!.ExerciseId);
        var cards = await service.GetWritingExerciseListViewModelAsync("beginner", "emails", null, 1, TestDataIds.UserId);
        Assert.Equal("openai", Assert.Single(cards.MyExercises).Origin!.GenerationMethod);
    }

    [Theory]
    [InlineData("advanced", "emails", "formal", "short")]
    [InlineData("beginner", "reports", "formal", "short")]
    [InlineData("beginner", "emails", "friendly", "short")]
    [InlineData("beginner", "emails", "formal", "long")]
    public async Task AiConstraintsNeverSilentlyDefault(string level, string type, string tone, string length)
    {
        await using var f = WritingBriefGenerationTests.Factory((_, _) => Task.FromResult(Reply(level, type, tone, length)));
        await WritingBriefGenerationTests.Premium(f);
        var result = await Create(f, Request());
        Assert.Equal(WritingCreateFromAiOutcome.TemplateOffer, result.Outcome); Assert.Equal("ai_constraint_mismatch", result.FallbackReason);
    }

    [Theory]
    [InlineData("refusal")]
    [InlineData("budget_exhausted")]
    [InlineData("corpus_stale")]
    public async Task RefusalOrApplicationGuardNeverOffersTemplate(string code)
    {
        await using var f = WritingBriefGenerationTests.Factory((_, _) => throw new AiProviderException("synthetic", code, false));
        await WritingBriefGenerationTests.Premium(f);
        var result = await Create(f, Request());
        Assert.Equal(WritingCreateFromAiOutcome.Failed, result.Outcome); Assert.Empty(result.Templates);
    }

    [Fact]
    public async Task SourceOutageAndUnsupportedFreeBriefNeverReplaceTextWithGenericTemplate()
    {
        await using var f = WritingBriefGenerationTests.Factory((_, _) => throw new AiProviderException("synthetic", "circuit_open", false));
        await WritingBriefGenerationTests.Premium(f);
        var source = await Create(f, new() { SourceText = "I need this whole source to remain faithfully aligned.", IdempotencyKey = "source-1" });
        Assert.Equal(WritingCreateFromAiOutcome.Failed, source.Outcome); Assert.Empty(source.Templates);
        var r = Request(); r.Brief = "Explain a fictional quantum physics laboratory discovery in detail.";
        var brief = await Create(f, r); Assert.Equal(WritingCreateFromAiOutcome.Failed, brief.Outcome);
        Assert.Equal("Unsupported", brief.Coverage); Assert.Empty(brief.Templates);
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("inactive")]
    [InlineData("level")]
    [InlineData("length")]
    [InlineData("tone")]
    [InlineData("brief")]
    [InlineData("key")]
    [InlineData("conflict")]
    public async Task InvalidInputNeverReachesProvider(string field)
    {
        var calls = 0;
        await using var f = WritingBriefGenerationTests.Factory((_, _) => { calls++; return Task.FromResult(Reply()); });
        await WritingBriefGenerationTests.Premium(f);
        var r = Request();
        switch (field) { case "mode": r.Mode = "translate"; break; case "inactive": r.SourceText = "other text"; break;
            case "level": r.Level = "expert"; break; case "length": r.Length = "huge"; break; case "tone": r.Tone = "rude"; break;
            case "brief": r.Brief = new string('!', 100); break; case "key": r.IdempotencyKey = ""; break;
            case "conflict": r.Brief += " level: advanced"; break; }
        Assert.Equal(WritingCreateFromAiOutcome.Invalid, (await Create(f, r)).Outcome); Assert.Equal(0, calls);
    }

    [Fact]
    public async Task RecoveryAndConfirmationEnforceRealOwnerAndAntiforgery()
    {
        await using var f = WritingBriefGenerationTests.Factory((_, _) => throw new AiProviderException("synthetic", "provider_unavailable", true));
        await WritingBriefGenerationTests.Premium(f);
        var op = await Create(f, Request()); var route = $"/api/writing/generation/{op.OperationId}";
        using var owner = IntegrationTestClientHelper.CreateAuthenticatedClient(f, TestDataIds.UserId, Roles.Premium);
        using var other = IntegrationTestClientHelper.CreateAuthenticatedClient(f, TestDataIds.OutsiderUserId, Roles.Premium);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(route + "/confirm-template", Choice())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await WritingBriefGenerationTests.Post(other, route + "/confirm-template", Choice())).StatusCode);
        using var success = await WritingBriefGenerationTests.Post(owner, route + "/confirm-template", Choice());
        Assert.Equal(HttpStatusCode.OK, success.StatusCode); Assert.True(success.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task CancellationAndRoleChangeFenceLateProviderResult()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var f = WritingBriefGenerationTests.Factory(async (_, ct) =>
        { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return Reply(); });
        await WritingBriefGenerationTests.Premium(f);
        using var scope = f.Services.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var running = scope.ServiceProvider.GetRequiredService<IWritingService>().CreateFromAiAsync(Request(), TestDataIds.UserId, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var op = await db.WritingGenerationOperations.AsNoTracking().SingleAsync();
        Assert.Equal("cancelled", op.State); Assert.False(op.Reserved); Assert.True(op.ProviderOutcomeUncertain);
        var result = await scope.ServiceProvider.GetRequiredService<IWritingService>().ConfirmWritingTemplateAsync(TestDataIds.UserId, op.Id, Choice());
        Assert.Equal("cancelled", result.Status);
        Assert.False(await db.WritingExercises.AnyAsync(e => e.UserId == TestDataIds.UserId));
    }
}
