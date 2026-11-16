using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingBriefUiRecoveryTests
{
    [Fact]
    public async Task LostFirstResponseCanRecoverByOwnedKeyWithoutAnotherProviderCall()
    {
        var calls = 0;
        await using var f = WritingBriefGenerationTests.Factory((_, _) => { calls++; throw new AiProviderException("synthetic", "circuit_open", false); });
        await WritingBriefGenerationTests.Premium(f);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(f, TestDataIds.UserId, Roles.Premium);
        using var created = await WritingBriefGenerationTests.Post(client, WritingBriefGenerationTests.CreateRoute,
            WritingBriefGenerationTests.Brief("lost-first-response"));
        var original = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        using var recovered = await client.GetAsync("/api/writing/generation/by-key/LOST-FIRST-RESPONSE");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode); Assert.True(recovered.Headers.CacheControl!.NoStore);
        var data = (await recovered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(original.GetProperty("operationId").GetString(), data.GetProperty("operationId").GetString());
        Assert.Equal("awaiting_template", data.GetProperty("status").GetString()); Assert.Equal(1, calls);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        (await db.Users.SingleAsync(u => u.UserId == TestDataIds.OutsiderUserId)).Role = Roles.Premium; await db.SaveChangesAsync();
        using var foreign = IntegrationTestClientHelper.CreateAuthenticatedClient(f, TestDataIds.OutsiderUserId, Roles.Premium);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync("/api/writing/generation/by-key/lost-first-response")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/writing/generation/by-key/missing-key")).StatusCode);
        Assert.False(await db.WritingGenerationLogs.AnyAsync(l => l.IsSuccess));
    }

    [Fact]
    public async Task QuotaReadUsesRealRoleSharedSuccessesAndUnexpiredReservationsWithoutLearningWrites()
    {
        await using var f = WritingBriefGenerationTests.Factory((_, _) => throw new InvalidOperationException("No provider on reads"));
        await WritingBriefGenerationTests.Premium(f);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var now = DateTime.UtcNow;
        db.WritingGenerationLogs.AddRange(Enumerable.Range(0, 3).Select(_ => new WritingGenerationLog
        { Id = Guid.NewGuid(), UserId = TestDataIds.UserId, RequestType = "create-from-ai", RequestedAtUtc = now, IsSuccess = true }));
        foreach (var expired in new[] { false, true }) db.WritingGenerationOperations.Add(new WritingGenerationOperation
        { Id = Guid.NewGuid(), UserId = TestDataIds.UserId, IdempotencyKey = "reserved-" + expired, PayloadHash = "synthetic",
            InputMode = "from_brief", State = "running", Reserved = true, QuotaDayUtc = now.Date, CreatedAtUtc = now,
            LeaseExpiresAtUtc = now.AddMinutes(expired ? -1 : 5) });
        await db.SaveChangesAsync();
        var before = await WritingRecentExercisesTests.RewardSnapshot(db);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(f, TestDataIds.UserId, Roles.Premium);
        using var response = await client.GetAsync("/api/writing/generation/quota");
        Assert.True(response.Headers.CacheControl!.NoStore);
        var quota = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(TestDataIds.UserId, quota.GetProperty("ownerUserId").GetInt32());
        Assert.Equal(5, quota.GetProperty("dailyLimit").GetInt32()); Assert.Equal(3, quota.GetProperty("succeeded").GetInt32());
        Assert.Equal(1, quota.GetProperty("reserved").GetInt32()); Assert.Equal(1, quota.GetProperty("remaining").GetInt32());
        Assert.Equal(now.Date.AddDays(1), quota.GetProperty("resetAtUtc").GetDateTime());
        Assert.Equal(before, await WritingRecentExercisesTests.RewardSnapshot(db));
        (await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId)).Role = Roles.Admin; await db.SaveChangesAsync();
        var admin = (await client.GetFromJsonAsync<JsonElement>("/api/writing/generation/quota")).GetProperty("data");
        Assert.True(admin.GetProperty("isUnlimited").GetBoolean()); Assert.Equal(JsonValueKind.Null, admin.GetProperty("remaining").ValueKind);
    }
}
