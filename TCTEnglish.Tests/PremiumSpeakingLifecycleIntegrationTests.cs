using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class PremiumSpeakingLifecycleIntegrationTests
{
    [Fact]
    public async Task Speaking_Index_StandardUser_SeesImportPanel_ButCreateIsBlockedWithUpgradePrompt()
    {
        var transcriptService = BuildTranscriptService();
        await using var factory = CreateFactoryWithTranscriptService(transcriptService);
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var indexBody = await client.GetStringAsync("/Speaking");
        Assert.Contains("id=\"mip-submit-btn\"", indexBody, StringComparison.Ordinal);

        using var createResponse = await PostCreateAsync(client, "https://www.youtube.com/watch?v=abc123DEF45");
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);

        var createJson = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("premium_required", createJson.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Speaking_Create_PremiumUser_EnforcesEnglishOnly_DuplicatePerOwner_AndTranscriptFallbackMetadata()
    {
        var transcriptService = BuildTranscriptService();
        await using var factory = CreateFactoryWithTranscriptService(transcriptService);
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);

        using var firstCreate = await PostCreateAsync(client, "https://www.youtube.com/watch?v=abc123DEF45");
        Assert.Equal(HttpStatusCode.OK, firstCreate.StatusCode);

        using var duplicateCreate = await PostCreateAsync(client, "https://youtu.be/abc123DEF45");
        Assert.Equal(HttpStatusCode.BadRequest, duplicateCreate.StatusCode);
        var duplicatePayload = await duplicateCreate.Content.ReadAsStringAsync();
        Assert.Contains("đã có trong mục Bài nói của tôi", duplicatePayload, StringComparison.OrdinalIgnoreCase);

        using var nonEnglishCreate = await PostCreateAsync(client, "https://www.youtube.com/watch?v=zzz999yyy88");
        Assert.Equal(HttpStatusCode.BadRequest, nonEnglishCreate.StatusCode);
        var nonEnglishPayload = await nonEnglishCreate.Content.ReadAsStringAsync();
        Assert.Contains("transcript tiếng Anh", nonEnglishPayload, StringComparison.OrdinalIgnoreCase);

        using var fallbackCreate = await PostCreateAsync(client, "https://www.youtube.com/watch?v=fbk111AAA22");
        Assert.Equal(HttpStatusCode.OK, fallbackCreate.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var created = await context.SpeakingVideos.AsNoTracking()
            .Where(video => video.OwnerUserId == TestDataIds.UserId)
            .OrderBy(video => video.YoutubeId)
            .Select(video => new { video.YoutubeId, video.TranscriptSource })
            .ToListAsync();

        Assert.Contains(created, item => item.YoutubeId == "abc123DEF45" && item.TranscriptSource == "youtube-captions");
        Assert.Contains(created, item => item.YoutubeId == "fbk111AAA22" && item.TranscriptSource == "gemini-fallback");
    }

    [Fact]
    public async Task Speaking_PrivateAccess_OutsiderCannotPracticeDeleteOrWriteProgress_ForAnotherOwnersPrivateSentence()
    {
        var transcriptService = BuildTranscriptService();
        await using var factory = CreateFactoryWithTranscriptService(transcriptService);
        await factory.InitializeAsync();

        var seeded = await SeedPrivateSpeakingVideoAsync(factory, TestDataIds.UserId);

        using var outsiderClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Premium);

        using var practiceResponse = await outsiderClient.GetAsync($"/Speaking/Practice/{seeded.VideoId}");
        Assert.Equal(HttpStatusCode.NotFound, practiceResponse.StatusCode);

        using var deleteResponse = await PostDeleteAsync(outsiderClient, seeded.VideoId);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        using var progressResponse = await PostSaveProgressAsync(outsiderClient, seeded.SentenceId);
        Assert.Equal(HttpStatusCode.NotFound, progressResponse.StatusCode);
    }

    [Fact]
    public async Task Speaking_DowngradedOwner_IsLockedAcrossListCreatePracticeDeleteAndProgressWrite()
    {
        var transcriptService = BuildTranscriptService();
        await using var factory = CreateFactoryWithTranscriptService(transcriptService);
        await factory.InitializeAsync();

        var seeded = await SeedPrivateSpeakingVideoAsync(factory, TestDataIds.UserId);

        using var ownerClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var indexBody = await ownerClient.GetStringAsync("/Speaking");
        Assert.Contains("Tính năng đã khóa", indexBody, StringComparison.Ordinal);

        using var createResponse = await PostCreateAsync(ownerClient, "https://www.youtube.com/watch?v=abc123DEF45");
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);

        using var practiceResponse = await ownerClient.GetAsync($"/Speaking/Practice/{seeded.VideoId}");
        Assert.Equal(HttpStatusCode.NotFound, practiceResponse.StatusCode);

        using var deleteResponse = await PostDeleteAsync(ownerClient, seeded.VideoId);
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        using var progressResponse = await PostSaveProgressAsync(ownerClient, seeded.SentenceId);
        Assert.Equal(HttpStatusCode.NotFound, progressResponse.StatusCode);
    }

    [Fact]
    public async Task Speaking_DeleteAccount_RemovesOwnedPrivateSpeakingVideosAndDependentProgress()
    {
        var transcriptService = BuildTranscriptService();
        await using var factory = CreateFactoryWithTranscriptService(transcriptService);
        await factory.InitializeAsync();
        await UpdateUserRoleAsync(factory, TestDataIds.UserId, Roles.Premium);

        var seeded = await SeedPrivateSpeakingVideoAsync(factory, TestDataIds.UserId);
        await SeedProgressForSentenceAsync(factory, seeded.SentenceId, TestDataIds.UserId);
        await SeedProgressForSentenceAsync(factory, seeded.SentenceId, TestDataIds.OutsiderUserId);

        using var ownerClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        using var deleteAccountResponse = await PostDeleteAccountAsync(ownerClient);

        Assert.Equal(HttpStatusCode.Redirect, deleteAccountResponse.StatusCode);
        Assert.Equal("/Account/Login", deleteAccountResponse.Headers.Location?.OriginalString);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        Assert.False(await context.Users.AsNoTracking().AnyAsync(user => user.UserId == TestDataIds.UserId));
        Assert.False(await context.SpeakingVideos.AsNoTracking().AnyAsync(video => video.Id == seeded.VideoId));
        Assert.False(await context.SpeakingSentences.AsNoTracking().AnyAsync(sentence => sentence.VideoId == seeded.VideoId));
        Assert.False(await context.UserSpeakingProgresses.AsNoTracking().AnyAsync(progress => progress.SentenceId == seeded.SentenceId));
    }

    private static TestWebApplicationFactory CreateFactoryWithTranscriptService(FakeYoutubeTranscriptService transcriptService)
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IYoutubeTranscriptService>();
            services.AddSingleton<IYoutubeTranscriptService>(transcriptService);
        });
    }

    private static FakeYoutubeTranscriptService BuildTranscriptService()
    {
        return new FakeYoutubeTranscriptService(
            new Dictionary<string, YoutubeTranscriptResult>
            {
                ["abc123DEF45"] = new YoutubeTranscriptResult(
                    BuildSentences("This transcript is clearly English and long enough for import validation."),
                    true,
                    "youtube-captions"),
                ["fbk111AAA22"] = new YoutubeTranscriptResult(
                    BuildSentences("Fallback transcript from Gemini should still be valid English for speaking practice."),
                    true,
                    "gemini-fallback"),
                ["zzz999yyy88"] = new YoutubeTranscriptResult(
                    new List<SpeakingSentence>(),
                    false,
                    null)
            });
    }

    private static List<SpeakingSentence> BuildSentences(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return
        [
            new SpeakingSentence
            {
                StartTime = 0,
                EndTime = 3,
                Text = string.Join(' ', parts.Take(Math.Max(3, parts.Length / 3))),
                VietnameseMeaning = string.Empty
            },
            new SpeakingSentence
            {
                StartTime = 3,
                EndTime = 6,
                Text = string.Join(' ', parts.Skip(Math.Max(3, parts.Length / 3)).Take(Math.Max(3, parts.Length / 3))),
                VietnameseMeaning = string.Empty
            },
            new SpeakingSentence
            {
                StartTime = 6,
                EndTime = 9,
                Text = string.Join(' ', parts.Skip(Math.Max(6, (parts.Length / 3) * 2))),
                VietnameseMeaning = string.Empty
            }
        ];
    }

    private static async Task UpdateUserRoleAsync(TestWebApplicationFactory factory, int userId, string role)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(item => item.UserId == userId);
        user.Role = role;
        await context.SaveChangesAsync();
    }

    private static async Task<SeededPrivateSpeakingVideo> SeedPrivateSpeakingVideoAsync(TestWebApplicationFactory factory, int ownerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var nextVideoId = (await context.SpeakingVideos.MaxAsync(item => (int?)item.Id) ?? 0) + 1;
        var nextSentenceId = (await context.SpeakingSentences.MaxAsync(item => (int?)item.Id) ?? 0) + 1;

        context.SpeakingVideos.Add(new SpeakingVideo
        {
            Id = nextVideoId,
            OwnerUserId = ownerUserId,
            PlaylistId = null,
            Title = "Private Speaking Lifecycle",
            YoutubeId = $"seed{nextVideoId:000000}",
            Level = null,
            Topic = null,
            ThumbnailUrl = "https://img.youtube.com/vi/abc123DEF45/hqdefault.jpg",
            Duration = "1:11",
            SourceUrl = "https://www.youtube.com/watch?v=abc123DEF45",
            SourceType = "premium-user-youtube",
            TranscriptSource = "youtube-captions",
            ImportStatus = "ready",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        });

        context.SpeakingSentences.Add(new SpeakingSentence
        {
            Id = nextSentenceId,
            VideoId = nextVideoId,
            StartTime = 0,
            EndTime = 4,
            Text = "This is a private sentence that should be owner-scoped.",
            VietnameseMeaning = string.Empty
        });

        await context.SaveChangesAsync();
        return new SeededPrivateSpeakingVideo(nextVideoId, nextSentenceId);
    }

    private static async Task SeedProgressForSentenceAsync(TestWebApplicationFactory factory, int sentenceId, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        context.UserSpeakingProgresses.Add(new UserSpeakingProgress
        {
            UserId = userId,
            SentenceId = sentenceId,
            TotalScore = 90,
            AccuracyScore = 90,
            FluencyScore = 90,
            CompletenessScore = 90,
            PracticedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(HttpClient client, string youtubeUrl)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Speaking");

        return await client.PostAsync(
            "/Speaking/My/Create",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                new KeyValuePair<string, string>("youtubeUrl", youtubeUrl)
            ]));
    }

    private static async Task<HttpResponseMessage> PostDeleteAsync(HttpClient client, int videoId)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Speaking");

        return await client.PostAsync(
            "/Speaking/My/Delete",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                new KeyValuePair<string, string>("id", videoId.ToString())
            ]));
    }

    private static async Task<HttpResponseMessage> PostSaveProgressAsync(HttpClient client, int sentenceId)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Speaking");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/speaking/{sentenceId}/progress")
        {
            Content = JsonContent.Create(new
            {
                totalScore = 88,
                accuracyScore = 87,
                fluencyScore = 86,
                completenessScore = 85
            })
        };

        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostDeleteAccountAsync(HttpClient client)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Account/Settings");

        return await client.PostAsync(
            "/Account/DeleteAccount",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
            ]));
    }

    private sealed record SeededPrivateSpeakingVideo(int VideoId, int SentenceId);

    private sealed class FakeYoutubeTranscriptService : IYoutubeTranscriptService
    {
        private readonly IReadOnlyDictionary<string, YoutubeTranscriptResult> _transcripts;

        public FakeYoutubeTranscriptService(IReadOnlyDictionary<string, YoutubeTranscriptResult> transcripts)
        {
            _transcripts = transcripts;
        }

        public Task<List<SpeakingSentence>> GetTranscriptAsync(string youtubeId)
        {
            var result = _transcripts.TryGetValue(youtubeId, out var transcript)
                ? transcript
                : new YoutubeTranscriptResult(new List<SpeakingSentence>(), false, null);

            return Task.FromResult(result.Sentences);
        }

        public Task<YoutubeTranscriptResult> GetTranscriptForSpeakingImportAsync(string youtubeId, CancellationToken ct = default)
        {
            var result = _transcripts.TryGetValue(youtubeId, out var transcript)
                ? transcript
                : new YoutubeTranscriptResult(new List<SpeakingSentence>(), false, null);

            return Task.FromResult(result);
        }

        public Task<string?> GetVideoDurationAsync(string youtubeId)
        {
            return Task.FromResult<string?>("2:22");
        }

        public Task<YoutubeVideoMetadata?> GetVideoMetadataAsync(string youtubeId)
        {
            return Task.FromResult<YoutubeVideoMetadata?>(new YoutubeVideoMetadata($"Imported {youtubeId}", YoutubeUrlHelper.BuildDefaultThumbnailUrl(youtubeId)));
        }
    }
}
