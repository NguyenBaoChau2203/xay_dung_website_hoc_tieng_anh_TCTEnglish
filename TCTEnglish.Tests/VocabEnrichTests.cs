using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public class VocabEnrichTests
{
    private const string GoodJson = """
        ```json
        {"is_valid_word":true,"did_you_mean":null,"lemma":"resilient","ipa_us":"rɪˈzɪliənt","ipa_uk":null,"cefr":"c1",
         "senses":[
           {"pos":["adj","bogus"],"meaning_vi":"kiên cường","meaning_en":"able to recover","examples":[
              {"text":"She is resilient.","vi":"Cô ấy kiên cường."},{"text":"B","vi":"b"},{"text":"C","vi":"c"},{"text":"D","vi":"d"}]},
           {"pos":"verb","meaning_vi":"thứ hai","examples":[]},
           {"pos":["noun"],"meaning_vi":"thứ ba","examples":[]},
           {"pos":["noun"],"meaning_vi":"thứ tư (bị cắt)","examples":[]}],
         "synonyms":["tough","hardy","strong","firm","sturdy","stable","extra"],"antonyms":["fragile","weak","brittle","soft","fifth"],
         "collocations":[{"text":"a","vi":"1"},{"text":"b"},{"text":"c"},{"text":"d"},{"text":"e"},{"text":"f"}],
         "word_family":[{"text":"resilience","pos":"noun","vi":"sự kiên cường"},{"text":"x","pos":"weird"}],
         "verb_forms":{"past":null,"past_participle":null,"ing":null,"third_person":null},
         "plural":null,"topic_vi":"Không có trong danh sách","emoji":"💪💪","image_query":"strong tree","memory_tip_vi":null}
        ```
        """;

    private static AiProviderReply Reply(string text) => new(text, 1, 1, 2, "stub", null);

    private static VocabSuggestService Service(Func<int, Task<AiProviderReply>> respond, Microsoft.Extensions.Caching.Memory.IMemoryCache? cache = null)
    {
        var calls = 0;
        var client = new StubAiProviderClient((_, _, _) => respond(Interlocked.Increment(ref calls)));
        return new VocabSuggestService(client, new NullHttpClientFactory(), new ConfigurationBuilder().Build(), NullLogger<VocabSuggestService>.Instance, cache);
    }

    [Fact]
    public async Task Enrich_ClampsAndValidates()
    {
        var service = Service(_ => Task.FromResult(Reply(GoodJson)));
        var response = await service.EnrichAsync("resilient");

        Assert.True(response.Success);
        var r = response.Result!;
        Assert.Equal("C1", r.Cefr);
        Assert.Equal("/rɪˈzɪliənt/", r.IpaUs);
        Assert.Null(r.IpaUk);
        Assert.Equal(3, r.Senses.Count);
        Assert.Equal(new[] { "adj" }, r.Senses[0].Pos.ToArray());
        Assert.Equal(new[] { "verb" }, r.Senses[1].Pos.ToArray());
        Assert.Equal(3, r.Senses[0].Examples.Count);
        Assert.Equal(6, r.Synonyms.Count);
        Assert.Equal(4, r.Antonyms.Count);
        Assert.Equal(5, r.Collocations.Count);
        Assert.Equal(2, r.WordFamily.Count);
        Assert.Null(r.WordFamily[1].Pos);
        Assert.Null(r.VerbForms);
        Assert.Equal("Khác", r.TopicVi);
        Assert.Null(r.Emoji);
        Assert.Equal("strong tree", r.ImageQuery);
    }

    [Fact]
    public async Task Enrich_RetriesOnceOnBadJson_ThenSucceeds_OrFails()
    {
        var calls = 0;
        var flaky = Service(n => { calls = n; return Task.FromResult(Reply(n == 1 ? "Sorry, here you go: not json" : GoodJson)); });
        Assert.True((await flaky.EnrichAsync("resilient")).Success);
        Assert.Equal(2, calls);

        calls = 0;
        var broken = Service(n => { calls = n; return Task.FromResult(Reply("{\"is_valid_word\":true,\"senses\":[]}")); });
        var failed = await broken.EnrichAsync("resilient");
        Assert.False(failed.Success);
        Assert.Equal(2, calls);
        Assert.False(string.IsNullOrWhiteSpace(failed.ErrorMessage));
    }

    [Fact]
    public async Task Enrich_InvalidWordReturnsSuggestionWithoutRetry()
    {
        var calls = 0;
        var service = Service(n => { calls = n; return Task.FromResult(Reply("{\"is_valid_word\":false,\"did_you_mean\":\"resilient\",\"senses\":[]}")); });
        var response = await service.EnrichAsync("resilent");
        Assert.True(response.Success);
        Assert.False(response.Result!.IsValidWord);
        Assert.Equal("resilient", response.Result.DidYouMean);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Enrich_RejectsEmptyTerm_AndMapsProviderErrors()
    {
        var service = Service(_ => throw new AiProviderException("quota", AiProviderException.ErrorCodeQuota, false));
        Assert.Equal("Nhập từ trước khi gợi ý.", (await service.EnrichAsync("  ")).ErrorMessage);
        var failure = await service.EnrichAsync("word");
        Assert.False(failure.Success);
        Assert.Equal(AiProviderErrorMessages.For(AiProviderException.ErrorCodeQuota), failure.ErrorMessage);
    }

    [Fact]
    public async Task Enrich_UsesTheCacheWhenOneIsAvailable()
    {
        var calls = 0;
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var service = Service(n => { calls = n; return Task.FromResult(Reply(GoodJson)); }, cache);
        await service.EnrichAsync("Resilient", "kiên cường");
        await service.EnrichAsync("resilient", "Kiên cường");
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Prompt_CarriesHintContextAndEveryTopic()
    {
        var prompt = VocabSuggestService.BuildEnrichPrompt("run", "chạy", "I run daily.");
        Assert.Contains("\"chạy\"", prompt);
        Assert.Contains("I run daily.", prompt);
        foreach (var topic in TCTEnglish.Services.Sets.SetTopics.All)
        {
            Assert.Contains(topic, prompt);
        }
    }

    [Fact]
    public async Task EnrichBatch_PartialFailure_ReturnsPerItemErrors_AndKeepsOrder()
    {
        var service = Service(_ => Task.FromResult(Reply(GoodJson)));
        var okOnly = await service.EnrichBatchAsync(new[]
        {
            new VocabEnrichBatchRequest { Term = "alpha" },
            new VocabEnrichBatchRequest { Term = "   " },
            new VocabEnrichBatchRequest { Term = "gamma" },
        });
        Assert.Equal(new[] { "alpha", "", "gamma" }, okOnly.Select(i => i.Term).ToArray());
        Assert.Equal(new[] { true, false, true }, okOnly.Select(i => i.Ok).ToArray());
        Assert.NotNull(okOnly[1].Error);

        await Assert.ThrowsAsync<ArgumentException>(() => service.EnrichBatchAsync(Enumerable.Range(0, 11).Select(i => new VocabEnrichBatchRequest { Term = "t" + i }).ToList()));
    }

    // ------------------------------------------------------------------ endpoints

    [Fact]
    public async Task Endpoints_NonPremiumGets403WithPremiumRequiredFlag_AndPremiumPasses()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IVocabSuggestService>();
            services.AddScoped<IVocabSuggestService, FakeVocabService>();
        });
        await factory.InitializeAsync();

        using var standard = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(standard, "/Home/CreateSet");

        using var denied = Post(standard, token, "/api/vocab/enrich", "{\"term\":\"word\"}");
        using var deniedResponse = await standard.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        var body = await deniedResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"premiumRequired\":true", body);

        using var deniedImages = await standard.GetAsync("/api/vocab/images?q=tree");
        Assert.Equal(HttpStatusCode.Forbidden, deniedImages.StatusCode);

        using var premium = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        var premiumToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(premium, "/Home/CreateSet");
        using var ok = Post(premium, premiumToken, "/api/vocab/enrich", "{\"term\":\"word\"}");
        using var okResponse = await premium.SendAsync(ok);
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);

        using var tooMany = Post(premium, premiumToken, "/api/vocab/enrich-batch",
            "{\"items\":[" + string.Join(",", Enumerable.Range(0, 11).Select(i => "{\"term\":\"t" + i + "\"}")) + "]}");
        using var tooManyResponse = await premium.SendAsync(tooMany);
        Assert.Equal(HttpStatusCode.BadRequest, tooManyResponse.StatusCode);

        using var noToken = new HttpRequestMessage(HttpMethod.Post, "/api/vocab/enrich") { Content = new StringContent("{\"term\":\"word\"}", Encoding.UTF8, "application/json") };
        using var noTokenResponse = await premium.SendAsync(noToken);
        Assert.Equal(HttpStatusCode.BadRequest, noTokenResponse.StatusCode);
    }

    private static HttpRequestMessage Post(HttpClient client, string token, string url, string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add("RequestVerificationToken", token);
        return request;
    }

    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class FakeVocabService : IVocabSuggestService
    {
        public Task<VocabSuggestionResult> SuggestAsync(string term, CancellationToken ct = default) => Task.FromResult(new VocabSuggestionResult { Success = true });
        public Task<VocabEnrichResponse> EnrichAsync(string term, string? meaningHint = null, string? context = null, CancellationToken ct = default)
            => Task.FromResult(new VocabEnrichResponse { Success = true, Result = new() { Lemma = term } });
        public Task<IReadOnlyList<VocabEnrichBatchItem>> EnrichBatchAsync(IReadOnlyList<VocabEnrichBatchRequest> items, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<VocabEnrichBatchItem>>(items.Select(i => new VocabEnrichBatchItem { Term = i.Term ?? "", Ok = true }).ToList());
        public Task<IReadOnlyList<ImageSearchHit>> SearchImagesAsync(string query, int max, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ImageSearchHit>>(Array.Empty<ImageSearchHit>());
    }
}
