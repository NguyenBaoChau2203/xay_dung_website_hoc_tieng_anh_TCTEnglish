using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Services.Sets;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public class PracticeIntegrationTests
{
    private static readonly string[] AllModes = { "learn", "test", "match", "flashcard", "quiz", "typing", "listening", "cloze" };

    private sealed record Seed(int SetId, int[] CardIds);

    private static async Task<TestWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        return factory;
    }

    private static HttpClient Client(TestWebApplicationFactory factory, int userId, string role = Roles.Standard)
        => IntegrationTestClientHelper.CreateAuthenticatedClient(factory, userId, role);

    /// <summary>Adds a set owned by <paramref name="ownerId"/> with <paramref name="terms"/> as cards (CreatedAt increases with the index).</summary>
    private static async Task<Seed> AddSetAsync(TestWebApplicationFactory factory, int ownerId, string name, string visibility, params string[] terms)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var set = new Set { SetName = name, OwnerId = ownerId, Visibility = visibility, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var baseTime = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < terms.Length; i++)
        {
            set.Cards.Add(new Card
            {
                Term = terms[i],
                Definition = "nghĩa " + terms[i],
                CreatedAt = baseTime.AddMinutes(i),
                PersonalNote = "PRIVATE-NOTE-" + terms[i],
                Example = "I like " + terms[i] + " very much.",
                ExampleTranslation = "Tôi thích " + terms[i],
            });
        }

        db.Sets.Add(set);
        await db.SaveChangesAsync();
        return new Seed(set.SetId, set.Cards.OrderBy(c => c.CardId).Select(c => c.CardId).ToArray());
    }

    private static async Task SetProgressAsync(TestWebApplicationFactory factory, int userId, int cardId,
        int wrong = 0, bool suspended = false, DateTime? next = null, DateTime? last = null, string status = "Learning")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.LearningProgresses.Add(new LearningProgress
        {
            UserId = userId,
            CardId = cardId,
            Status = status,
            WrongCount = wrong,
            RepetitionCount = 0,
            NextReviewDate = next,
            LastReviewedDate = last,
            IsSuspended = suspended,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<(HttpStatusCode Status, string Body)> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static JsonElement Config(string html)
    {
        var match = Regex.Match(html, "<script id=\"tpConfig\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(match.Success, "tpConfig block is missing");
        return JsonDocument.Parse(match.Groups[1].Value).RootElement.Clone();
    }

    private static async Task<JsonElement> SessionAsync(HttpClient client, string url)
    {
        var (status, body) = await GetAsync(client, url);
        Assert.True(status == HttpStatusCode.OK, $"{url} -> {(int)status}\n{body[..Math.Min(body.Length, 500)]}");
        return Config(body);
    }

    private static int[] Ids(JsonElement config, string array)
        => config.GetProperty(array).EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToArray();

    // ------------------------------------------------------------------ access

    [Fact]
    public async Task Owner_OpensHub_HardWords_AndEveryMode_AndPagesCarryTheContract()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Owner practice set", SetVisibility.Private, "alpha", "beta", "gamma", "delta");
        using var owner = Client(factory, TestDataIds.UserId);

        foreach (var url in new[] { "/Practice", $"/Practice?set={seed.SetId}", "/Practice/HardWords" })
        {
            var (status, body) = await GetAsync(owner, url);
            Assert.True(status == HttpStatusCode.OK, $"{url} -> {(int)status}");
            Assert.Contains("class=\"tct-theme tct-page tp-page tp-page--", body);
        }

        foreach (var mode in AllModes)
        {
            var (status, body) = await GetAsync(owner, $"/Practice/{mode}?set={seed.SetId}");
            Assert.True(status == HttpStatusCode.OK, $"{mode} -> {(int)status}");
            Assert.Contains("name=\"__RequestVerificationToken\"", body);
            var expectedFeature = mode == "flashcard" ? "Flashcard" : "Quiz";
            Assert.Contains($"data-feature=\"{expectedFeature}\"", body);
            Assert.Contains("src=\"/js/time-tracker.js", body);
            Assert.Single(Regex.Matches(body, "<h1[ >]"));
            var config = Config(body);
            Assert.Equal(mode, config.GetProperty("mode").GetString());
            Assert.Equal($"set:{seed.SetId}", config.GetProperty("scopeKey").GetString());
            Assert.Equal(TestDataIds.UserId, config.GetProperty("userId").GetInt32());
        }

        // Mode is case-insensitive and canonicalized.
        var upper = await SessionAsync(owner, $"/Practice/LEARN?set={seed.SetId}");
        Assert.Equal("learn", upper.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Outsider_GetsNotFoundForEveryModeOfAPrivateSet_AndHubDoesNotLeakIt()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Secret practice deck", SetVisibility.Private, "alpha", "beta", "gamma");
        using var outsider = Client(factory, TestDataIds.OutsiderUserId);

        foreach (var mode in AllModes)
        {
            var (status, _) = await GetAsync(outsider, $"/Practice/{mode}?set={seed.SetId}");
            Assert.True(status == HttpStatusCode.NotFound, $"{mode} -> {(int)status}");
        }

        var (hubStatus, hubBody) = await GetAsync(outsider, $"/Practice?set={seed.SetId}");
        Assert.Equal(HttpStatusCode.OK, hubStatus);
        Assert.DoesNotContain("Secret practice deck", hubBody);

        var (hardStatus, hardBody) = await GetAsync(outsider, $"/Practice/HardWords?set={seed.SetId}");
        Assert.Equal(HttpStatusCode.OK, hardStatus);
        Assert.DoesNotContain("Secret practice deck", hardBody);
    }

    [Fact]
    public async Task Stranger_CanPracticeAPublicSet_UnknownModeIs404_AnonymousIsRedirected()
    {
        await using var factory = await CreateFactoryAsync();
        using var stranger = Client(factory, TestDataIds.OutsiderUserId);

        var (status, body) = await GetAsync(stranger, $"/Practice/learn?set={TestDataIds.SystemSetId}");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(Config(body).GetProperty("empty").GetBoolean());

        var (hubStatus, _) = await GetAsync(stranger, $"/Practice?set={TestDataIds.SystemSetId}");
        Assert.Equal(HttpStatusCode.OK, hubStatus);

        var (unknown, _) = await GetAsync(stranger, "/Practice/chinese?set=" + TestDataIds.SystemSetId);
        Assert.Equal(HttpStatusCode.NotFound, unknown);

        using var anonymous = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        foreach (var url in new[] { "/Practice", "/Practice/learn", "/Practice/HardWords" })
        {
            using var response = await anonymous.GetAsync(url);
            // Cookie auth challenges with a login redirect; the test host may answer 401 instead. Both mean "not served".
            Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, $"{url} -> {(int)response.StatusCode}");
        }
    }

    // ------------------------------------------------------------------ payload

    [Fact]
    public async Task Payload_ExcludesSuspended_NeverLeaksPersonalNote_AndPoolExcludesTargets()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Payload set", SetVisibility.Private,
            "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve");
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[1], suspended: true);
        using var owner = Client(factory, TestDataIds.UserId);

        var (_, html) = await GetAsync(owner, $"/Practice/quiz?set={seed.SetId}&n=10");
        Assert.DoesNotContain("PRIVATE-NOTE", html);
        Assert.DoesNotContain("personalNote", html, StringComparison.OrdinalIgnoreCase);
        var config = Config(html);

        var targets = Ids(config, "targets");
        var pool = Ids(config, "pool");
        Assert.Equal(10, targets.Length);
        Assert.DoesNotContain(seed.CardIds[1], targets);
        Assert.DoesNotContain(seed.CardIds[1], pool);
        Assert.Empty(targets.Intersect(pool));
        // "all" source keeps set order (CreatedAt, then CardId).
        Assert.Equal(targets.OrderBy(id => Array.IndexOf(seed.CardIds, id)).ToArray(), targets);
        // 11 usable cards, 10 targets -> exactly one distractor in the pool, then topped up from other viewable sets.
        Assert.Contains(pool, id => id == seed.CardIds.Except(targets).Except(new[] { seed.CardIds[1] }).Single());

        var first = config.GetProperty("targets")[0];
        Assert.Equal("I like one very much.", first.GetProperty("examples")[0].GetProperty("text").GetString());
        Assert.Equal("new", first.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sources_AreOrdered_AndWordsOverridesSource()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Source set", SetVisibility.Private, "a1", "a2", "a3", "a4", "a5");
        var now = DateTime.UtcNow;
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[0], wrong: 2, next: now.AddDays(-1), last: now.AddHours(-5));
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[1], wrong: 5, next: now.AddDays(-3), last: now.AddHours(-9));
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[2], wrong: 5, next: now.AddDays(+3), last: now.AddHours(-1));
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[3], wrong: 0, next: now.AddDays(-2), last: now.AddHours(-2), status: "Mastered");
        using var owner = Client(factory, TestDataIds.UserId);

        var due = Ids(await SessionAsync(owner, $"/Practice/quiz?set={seed.SetId}&source=due"), "targets");
        Assert.Equal(new[] { seed.CardIds[1], seed.CardIds[3], seed.CardIds[0] }, due);

        var hard = Ids(await SessionAsync(owner, $"/Practice/quiz?set={seed.SetId}&source=hard"), "targets");
        // wrong desc, then last reviewed desc: card[2] (5, newer) before card[1] (5, older), then card[0] (2).
        Assert.Equal(new[] { seed.CardIds[2], seed.CardIds[1], seed.CardIds[0] }, hard);

        var recent = Ids(await SessionAsync(owner, $"/Practice/quiz?set={seed.SetId}&source=recent"), "targets");
        Assert.Equal(seed.CardIds.Reverse().ToArray(), recent);

        // words= keeps the given order and drops ids the user cannot view (other users' private set).
        var foreign = await AddSetAsync(factory, TestDataIds.AdminUserId, "Admin private", SetVisibility.Private, "x1");
        var csv = $"{seed.CardIds[3]},{foreign.CardIds[0]},{seed.CardIds[0]},{seed.CardIds[3]},999999";
        var words = await SessionAsync(owner, $"/Practice/learn?words={csv}&source=due");
        Assert.Equal(new[] { seed.CardIds[3], seed.CardIds[0] }, Ids(words, "targets"));
        Assert.StartsWith("words:", words.GetProperty("scopeKey").GetString());
    }


    [Fact]
    public async Task SessionShell_LoadsTheRightScripts_AndEmptySessionsRenderAStateInstead()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Shell set", SetVisibility.Private, "s1", "s2", "s3");
        using var owner = Client(factory, TestDataIds.UserId);

        foreach (var (mode, script) in new[] { ("quiz", "practice-drill.js"), ("typing", "practice-drill.js"), ("listening", "practice-drill.js"),
                     ("cloze", "practice-drill.js"), ("learn", "practice-learn.js"), ("test", "practice-test.js"), ("match", "practice-match.js"), ("flashcard", "practice-flashcard.js") })
        {
            var (_, body) = await GetAsync(owner, $"/Practice/{mode}?set={seed.SetId}");
            Assert.Contains("/js/practice-core.js", body);
            Assert.Contains("/js/practice-ui.js", body);
            Assert.Contains("/js/" + script, body);
            Assert.Contains("id=\"tpExitModal\"", body);
            Assert.Contains("id=\"tpProgress\" role=\"progressbar\"", body);
        }

        var (_, due) = await GetAsync(owner, $"/Practice/learn?set={seed.SetId}&source=due");
        Assert.Contains("tct-state--empty", due);
        Assert.DoesNotContain("practice-ui.js", due);
        Assert.Contains($"href=\"/Practice/learn?set={seed.SetId}&amp;n=10\"", due); // "Luyện tất cả": same session without the due source
        Assert.DoesNotContain("id=\"tpStage\"", due);
    }

    [Fact]
    public async Task EmptyStates_AreServedAsEmptySessions_NotErrors()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Empty source set", SetVisibility.Private, "e1", "e2", "e3");
        using var owner = Client(factory, TestDataIds.UserId);

        var due = await SessionAsync(owner, $"/Practice/learn?set={seed.SetId}&source=due");
        Assert.True(due.GetProperty("empty").GetBoolean());
        Assert.Equal("due", due.GetProperty("emptyReason").GetString());

        var hard = await SessionAsync(owner, $"/Practice/quiz?set={seed.SetId}&source=hard");
        Assert.Equal("hard", hard.GetProperty("emptyReason").GetString());

        var blank = await AddSetAsync(factory, TestDataIds.UserId, "Blank set", SetVisibility.Private);
        var none = await SessionAsync(owner, $"/Practice/quiz?set={blank.SetId}");
        Assert.True(none.GetProperty("empty").GetBoolean());
        Assert.Equal("none", none.GetProperty("emptyReason").GetString());
    }

    [Fact]
    public async Task Query_IsClampedAndNormalized_NeverAnError()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Clamp set", SetVisibility.Private,
            Enumerable.Range(1, 30).Select(i => "w" + i).ToArray());
        using var owner = Client(factory, TestDataIds.UserId);

        var learn = await SessionAsync(owner, $"/Practice/learn?set={seed.SetId}&n=7&source=zzz&srs=2&from=elsewhere");
        var query = learn.GetProperty("query");
        Assert.Equal(10, query.GetProperty("n").GetInt32());
        Assert.Equal("all", query.GetProperty("source").GetString());
        Assert.False(learn.GetProperty("countsToSrs").GetBoolean());
        Assert.Equal(10, learn.GetProperty("targets").GetArrayLength());

        var flash = await SessionAsync(owner, $"/Practice/flashcard?set={seed.SetId}&n=0");
        Assert.Equal(30, flash.GetProperty("targets").GetArrayLength());

        var match = await SessionAsync(owner, $"/Practice/match?set={seed.SetId}&n=12&srs=1");
        Assert.Equal(12, match.GetProperty("targets").GetArrayLength());
        Assert.False(match.GetProperty("countsToSrs").GetBoolean()); // Match never writes progress.

        var srs = await SessionAsync(owner, $"/Practice/quiz?set={seed.SetId}&n=20&srs=1");
        Assert.True(srs.GetProperty("countsToSrs").GetBoolean());
        Assert.Equal(20, srs.GetProperty("targets").GetArrayLength());

        var test = await SessionAsync(owner, $"/Practice/test?set={seed.SetId}&n=10&types=cloze,choice,bogus&ask=bad");
        Assert.Equal("choice,cloze", test.GetProperty("query").GetProperty("types").GetString());
        Assert.Equal("both", test.GetProperty("query").GetProperty("ask").GetString());
        Assert.Equal(20, test.GetProperty("targets").GetArrayLength()); // cloze selected -> 2n targets
        Assert.EndsWith("/Practice?set=" + seed.SetId, test.GetProperty("exitUrl").GetString());

        var fromSet = await SessionAsync(owner, $"/Practice/learn?set={seed.SetId}&from=set");
        Assert.Equal($"/Sets/{seed.SetId}", fromSet.GetProperty("exitUrl").GetString());
        var fromHard = await SessionAsync(owner, $"/Practice/learn?set={seed.SetId}&from=hard");
        Assert.Equal("/Practice/HardWords", fromHard.GetProperty("exitUrl").GetString());
    }

    [Fact]
    public async Task TinyPool_IsToppedUpFromOtherViewableSets()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Two word set", SetVisibility.Private, "solo1", "solo2");
        using var owner = Client(factory, TestDataIds.UserId);

        var config = await SessionAsync(owner, $"/Practice/quiz?set={seed.SetId}&n=10");
        Assert.Equal(2, config.GetProperty("targets").GetArrayLength());
        var pool = config.GetProperty("pool").EnumerateArray().ToList();
        Assert.NotEmpty(pool); // the seeded public sets supply distractors
        Assert.All(pool, p => Assert.NotEqual(seed.SetId, p.GetProperty("setId").GetInt32()));
    }

    // ------------------------------------------------------------------ scope "mine"

    [Fact]
    public async Task ScopeMine_IncludesOwnAndFollowedSets_AndExcludesOthers()
    {
        await using var factory = await CreateFactoryAsync();
        var own = await AddSetAsync(factory, TestDataIds.UserId, "Mine set", SetVisibility.Private, "m1", "m2");
        var followed = await AddSetAsync(factory, TestDataIds.AdminUserId, "Followed public set", SetVisibility.Public, "f1", "f2");
        var other = await AddSetAsync(factory, TestDataIds.AdminUserId, "Someone else public set", SetVisibility.Public, "o1", "o2");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.SetSubscriptions.Add(new SetSubscription { UserId = TestDataIds.UserId, SetId = followed.SetId, SubscribedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var owner = Client(factory, TestDataIds.UserId);
        var config = await SessionAsync(owner, "/Practice/quiz?n=30");
        Assert.Equal("mine", config.GetProperty("scopeKey").GetString());
        var targetSets = config.GetProperty("targets").EnumerateArray().Select(t => t.GetProperty("setId").GetInt32()).Distinct().ToList();
        Assert.Contains(own.SetId, targetSets);
        Assert.Contains(followed.SetId, targetSets);
        Assert.DoesNotContain(other.SetId, targetSets);
        // Newest set first when grouped.
        Assert.True(config.GetProperty("targets")[0].GetProperty("setId").GetInt32() >= targetSets.Min());

        var (_, hub) = await GetAsync(owner, "/Practice");
        Assert.Contains("Mine set", hub);
        Assert.Contains("Followed public set", hub);
        Assert.DoesNotContain("Someone else public set", hub);
    }

    // ------------------------------------------------------------------ hard words

    [Fact]
    public async Task HardWords_OrdersByMissesAndRecency_UsesTheThreshold_AndSkipsSuspended()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Hard set", SetVisibility.Private, "h0", "h1", "h2", "h3", "h4");
        var now = DateTime.UtcNow;
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[0], wrong: 0, last: now);
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[1], wrong: 1, last: now.AddHours(-2));
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[2], wrong: 4, last: now.AddHours(-9));
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[3], wrong: 4, last: now.AddHours(-1));
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[4], wrong: 9, last: now, suspended: true);
        using var owner = Client(factory, TestDataIds.UserId);

        var (status, body) = await GetAsync(owner, "/Practice/HardWords");
        Assert.Equal(HttpStatusCode.OK, status);
        var positions = new[] { "h3", "h2", "h1" }.Select(t => body.IndexOf(">" + t + "<", StringComparison.Ordinal)).ToArray();
        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.True(positions[0] < positions[1] && positions[1] < positions[2], "order: 4 misses (newer), 4 misses (older), 1 miss");
        Assert.DoesNotContain(">h0<", body);
        Assert.DoesNotContain(">h4<", body);

        using var outsider = Client(factory, TestDataIds.OutsiderUserId);
        var (_, otherBody) = await GetAsync(outsider, "/Practice/HardWords");
        Assert.DoesNotContain(">h3<", otherBody);
    }


    // ------------------------------------------------------------------ integration with the set pages

    [Fact]
    public async Task SetPages_LinkToTheNewModes_AndTheLayoutHasNoPracticeEntryWithoutD1()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Linked set", SetVisibility.Private, "k1", "k2", "k3");
        using var owner = Client(factory, TestDataIds.UserId);

        var (_, detail) = await GetAsync(owner, $"/Sets/{seed.SetId}");
        foreach (var href in new[]
                 {
                     $"/Practice/learn?set={seed.SetId}&amp;n=20&amp;from=set",
                     $"/Practice/test?set={seed.SetId}&amp;n=10&amp;from=set",
                     $"/Practice/match?set={seed.SetId}&amp;n=6&amp;from=set",
                     $"/Practice/flashcard?set={seed.SetId}&amp;n=0&amp;from=set",
                     $"/Practice?set={seed.SetId}",
                 })
        {
            Assert.Contains($"href=\"{href}\"", detail);
        }

        Assert.Contains($"data-user-id=\"{TestDataIds.UserId}\"", detail);
        Assert.Contains("\"userId\":" + TestDataIds.UserId, detail);
        Assert.DoesNotContain($"/Home/QuizMode/{seed.SetId}", detail);
        Assert.DoesNotContain($"/Home/WriteMode/{seed.SetId}", detail);
        Assert.DoesNotContain($"/Home/MatchingMode/{seed.SetId}", detail);
        Assert.Contains("Luyện tập", detail);

        var (_, list) = await GetAsync(owner, "/Sets");
        Assert.Contains("href=\"/Practice\"", list);

        var (_, card) = await GetAsync(owner, $"/Sets/Cards/{seed.CardIds[0]}");
        Assert.Contains($"href=\"/Practice/learn?words={seed.CardIds[0]}&amp;from=set\"", card);
        Assert.Contains($"href=\"/Practice/typing?words={seed.CardIds[0]}\"", card);

        // D1 was not granted: the shared layout (any page without its own Practice link) has no sidebar item.
        var (_, home) = await GetAsync(owner, "/Home/Index");
        Assert.DoesNotContain("/Practice", home);
    }

    [Fact]
    public async Task Hub_PreselectsAViewableSet_AndHardWordsShowsTheStackedRowContract()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Hub pick set", SetVisibility.Private, "x1", "x2");
        await SetProgressAsync(factory, TestDataIds.UserId, seed.CardIds[0], wrong: 2, last: DateTime.UtcNow);
        using var owner = Client(factory, TestDataIds.UserId);

        var (_, hub) = await GetAsync(owner, $"/Practice?set={seed.SetId}");
        Assert.Contains($"<option value=\"{seed.SetId}\" selected=\"selected\">Hub pick set (2)", hub);
        Assert.Contains("id=\"tpHubConfig\"", hub);
        Assert.Contains("/js/practice-hub.js", hub);
        Assert.Contains("Từ hay quên", hub);
        foreach (var label in new[] { "Học", "Kiểm tra", "Ghép thẻ", "Lật thẻ", "Trắc nghiệm", "Gõ từ", "Nghe", "Điền từ" })
        {
            Assert.Contains(">" + label + "<", System.Net.WebUtility.HtmlDecode(hub));
        }

        var (_, hard) = await GetAsync(owner, "/Practice/HardWords");
        Assert.Contains("data-label=\"Số lần sai\"", hard);
        Assert.Contains($"href=\"/Practice/learn?words={seed.CardIds[0]}&amp;from=hard\"", hard);
        Assert.Contains($"href=\"/Practice/test?words={seed.CardIds[0]}&amp;from=hard\"", hard);

        // A user with no words sees the empty state instead of the setup panel.
        using var outsider = Client(factory, TestDataIds.OutsiderUserId);
        var (_, empty) = await GetAsync(outsider, "/Practice");
        Assert.Contains("tct-state--empty", empty);
    }

    // ------------------------------------------------------------------ contracts

    [Fact]
    public async Task ProgressEndpoint_StillRequiresTheAntiforgeryHeader()
    {
        await using var factory = await CreateFactoryAsync();
        var seed = await AddSetAsync(factory, TestDataIds.UserId, "Progress set", SetVisibility.Private, "p1", "p2");
        using var owner = Client(factory, TestDataIds.UserId);

        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(owner, $"/Practice/learn?set={seed.SetId}");
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var noHeader = new HttpRequestMessage(HttpMethod.Post, "/Home/UpdateCardProgress")
        {
            Content = new StringContent($"{{\"cardId\":{seed.CardIds[0]},\"isKnown\":true}}", System.Text.Encoding.UTF8, "application/json")
        };
        using var rejected = await owner.SendAsync(noHeader);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        using var withHeader = new HttpRequestMessage(HttpMethod.Post, "/Home/UpdateCardProgress")
        {
            Content = new StringContent($"{{\"cardId\":{seed.CardIds[0]},\"isKnown\":true}}", System.Text.Encoding.UTF8, "application/json")
        };
        withHeader.Headers.Add("RequestVerificationToken", token);
        using var accepted = await owner.SendAsync(withHeader);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public void CountsFor_MatchesThePlanTable()
    {
        Assert.Equal(new[] { 20, 50, 0 }, PracticeModes.CountsFor("flashcard"));
        Assert.Equal(new[] { 6, 12, 18 }, PracticeModes.CountsFor("match"));
        Assert.Equal(new[] { 10, 20, 50 }, PracticeModes.CountsFor("learn"));
        foreach (var mode in new[] { "test", "quiz", "typing", "listening", "cloze" })
        {
            Assert.Equal(new[] { 10, 20, 30 }, PracticeModes.CountsFor(mode));
        }
    }
}
