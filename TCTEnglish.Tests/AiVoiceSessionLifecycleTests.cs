using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.AI.Voice;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels.AI.Voice;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiVoiceSessionLifecycleTests
{
    [Fact]
    public async Task StopDuringCreateWinsAndLateProviderCallIsClosed()
    {
        var fake = new FakeVoiceProvider { PauseCreate = true };
        await using var factory = VoiceFactory(fake); await factory.InitializeAsync();
        var topic = await SeedTopic(factory);
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var id = Guid.NewGuid();
        var start = a.ServiceProvider.GetRequiredService<AiVoiceSessionService>().StartAsync(TestDataIds.UserId, Input(id, topic), default);
        await fake.Dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(await b.ServiceProvider.GetRequiredService<AiVoiceSessionService>().CloseAsync(TestDataIds.UserId, id, "stopped", default));
        fake.Continue.TrySetResult();
        await Assert.ThrowsAsync<VoiceException>(() => start);
        Assert.Equal(1, fake.Hangups);
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Empty(await db.AiVoiceUserLeases.ToListAsync());
        Assert.Equal("closed", (await db.AiVoiceSessions.FindAsync(id))!.State);
    }

    [Fact]
    public async Task SidebandFailureKeepsCallIdAndRetriesFailedHangup()
    {
        var fake = new FakeVoiceProvider { FailAttach = true, FailHangup = true };
        await using var factory = VoiceFactory(fake); await factory.InitializeAsync();
        var topic = await SeedTopic(factory); var id = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AiVoiceSessionService>();
        await Assert.ThrowsAsync<VoiceException>(() => service.StartAsync(TestDataIds.UserId, Input(id, topic), default));
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var row = await db.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.NotNull(row.ProviderCallId); Assert.Equal("pending", row.AccountingState);
        Assert.True(await db.AiVoiceUserLeases.AnyAsync());
        fake.FailHangup = false;
        Assert.True(await service.CloseAsync(TestDataIds.UserId, id, "retry", default));
        Assert.Equal(2, fake.Hangups);
    }

    [Fact]
    public async Task RepeatedStopKeepsConfirmedClosedSessionSettledWithoutAnotherHangup()
    {
        var fake = new FakeVoiceProvider(); await using var f = VoiceFactory(fake); await f.InitializeAsync();
        var topic = await SeedTopic(f); var id = Guid.NewGuid(); using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AiVoiceSessionService>();
        await service.StartAsync(TestDataIds.UserId, Input(id, topic), default);
        Assert.True(await service.CloseAsync(TestDataIds.UserId, id, "stopped", default));
        Assert.True(await service.CloseAsync(TestDataIds.UserId, id, "stopped", default));
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var row = await db.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("closed", row.State); Assert.Equal("settled", row.AccountingState); Assert.False(row.NeedsHangup);
        Assert.Equal(1, fake.Hangups); Assert.Empty(await db.AiVoiceUserLeases.ToListAsync());
    }

    [Fact]
    public async Task ReplayedFinalTranscriptAndUsageAreDeduplicatedAndInterruptionIsPreserved()
    {
        var fake = new FakeVoiceProvider(); await using var factory = VoiceFactory(fake); await factory.InitializeAsync();
        var topic = await SeedTopic(factory); var id = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<AiVoiceSessionService>();
        await session.StartAsync(TestDataIds.UserId, Input(id, topic), default);
        var processor = scope.ServiceProvider.GetRequiredService<AiVoiceEventProcessor>();
        await processor.HandleAsync(id, JsonSerializer.SerializeToElement(new { type = "output_audio_buffer.started", response_id = "resp_1" }), default);
        await processor.HandleAsync(id, JsonSerializer.SerializeToElement(new { type = "output_audio_buffer.cleared" }), default);
        var transcript = JsonSerializer.SerializeToElement(new { type = "response.output_audio_transcript.done", item_id = "item_1", response_id = "resp_1", transcript = "Hello, how are you?" });
        await processor.HandleAsync(id, transcript, default); await processor.HandleAsync(id, transcript, default);
        var usage = JsonSerializer.SerializeToElement(new { type = "response.done", response = new { id = "resp_1", status = "completed", usage = new { input_token_details = new { audio_tokens = 20, text_tokens = 3 }, output_token_details = new { audio_tokens = 8, text_tokens = 2 } } } });
        await processor.HandleAsync(id, usage, default); await processor.HandleAsync(id, usage, default);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.True((await db.AiVoiceTurns.AsNoTracking().SingleAsync()).Interrupted);
        var day = await db.AiVoiceUsageDays.AsNoTracking().SingleAsync(); Assert.Equal(20, day.InputAudioTokens); Assert.Equal(8, day.OutputAudioTokens);
        await session.CloseAsync(TestDataIds.UserId, id, "stopped", default);
    }

    internal static TestWebApplicationFactory VoiceFactory(FakeVoiceProvider fake) => new(services =>
    {
        services.RemoveAll<IRealtimeVoiceClient>(); services.AddSingleton<IRealtimeVoiceClient>(fake);
        services.Configure<AiVoiceOptions>(o => o.Enabled = true);
    });

    [Fact]
    public async Task NoiseDuringPlaybackDoesNotInterruptOrReopenListeningBeforeOutputDrains()
    {
        var fake = new FakeVoiceProvider(); await using var f = VoiceFactory(fake); await f.InitializeAsync();
        var topic = await SeedTopic(f); var id = Guid.NewGuid(); using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AiVoiceSessionService>();
        await service.StartAsync(TestDataIds.UserId, Input(id, topic), default);
        var processor = scope.ServiceProvider.GetRequiredService<AiVoiceEventProcessor>();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await processor.HandleAsync(id, JsonSerializer.SerializeToElement(new { type = "output_audio_buffer.started", response_id = "resp_noise" }), default);
        foreach (var type in new[] { "input_audio_buffer.speech_started", "input_audio_buffer.speech_stopped" })
            await processor.HandleAsync(id, JsonSerializer.SerializeToElement(new { type }), default);
        await processor.HandleAsync(id, JsonSerializer.SerializeToElement(new { type = "response.output_audio_transcript.done", item_id = "item_noise", response_id = "resp_noise", transcript = "Complete answer." }), default);
        await processor.HandleAsync(id, JsonSerializer.SerializeToElement(new { type = "response.done", response = new { id = "resp_noise", status = "completed" } }), default);
        var playing = await db.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("speaking", playing.AgentState); Assert.Equal("resp_noise", playing.PlayingResponseId);
        Assert.False((await db.AiVoiceTurns.AsNoTracking().SingleAsync()).Interrupted);
        await processor.HandleAsync(id, JsonSerializer.SerializeToElement(new { type = "output_audio_buffer.stopped", response_id = "resp_noise" }), default);
        var finished = await db.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("listening", finished.AgentState); Assert.Null(finished.PlayingResponseId);
        await service.CloseAsync(TestDataIds.UserId, id, "stopped", default);
    }

    [Fact]
    public async Task RecoverySweepClosesPersistedCallWithoutCreatingAnotherCall()
    {
        var fake = new FakeVoiceProvider(); await using var f = VoiceFactory(fake); await f.InitializeAsync();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var quota = scope.ServiceProvider.GetRequiredService<AiVoiceQuotaService>(); var id = Guid.NewGuid();
        await quota.ReserveAsync(TestDataIds.UserId, id, AiVoicePersistenceTests.Topic, false, default);
        await quota.MutateAsync(id, s => { s.ProviderCallId = "rtc_before_restart"; s.ProviderDispatched = true; s.SetupFinished = true;
            s.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2); s.HeartbeatAtUtc = DateTime.UtcNow.AddMinutes(-1); s.State = "active"; }, default);
        var worker = f.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<TCTEnglish.Workers.AiVoiceSessionCleanupWorker>().Single();
        await worker.SweepAsync(default);
        Assert.Equal(0, fake.Creates); Assert.Equal(1, fake.Hangups);
        Assert.Equal("closed", (await db.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == id)).State);
    }

    [Fact]
    public async Task StopDuringPlaybackMarksAlreadyGeneratedAssistantTurnInterrupted()
    {
        var fake = new FakeVoiceProvider(); await using var f = VoiceFactory(fake); await f.InitializeAsync();
        var topic = await SeedTopic(f); var id = Guid.NewGuid(); using var scope = f.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AiVoiceSessionService>(); await service.StartAsync(TestDataIds.UserId, Input(id, topic), default);
        var processor = scope.ServiceProvider.GetRequiredService<AiVoiceEventProcessor>();
        await processor.HandleAsync(id, JsonSerializer.SerializeToElement(new { type = "response.output_audio_transcript.done", item_id = "item_full", response_id = "resp_full", transcript = "A long answer still being played." }), default);
        await processor.HandleAsync(id, JsonSerializer.SerializeToElement(new { type = "output_audio_buffer.started", response_id = "resp_full" }), default);
        await service.CloseAsync(TestDataIds.UserId, id, "stopped", default);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>(); Assert.True((await db.AiVoiceTurns.AsNoTracking().SingleAsync()).Interrupted);
    }
    internal static async Task<int> SeedTopic(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await AiVoiceCatalogSeed.SeedAsync(db); return (await db.AiVoiceCatalogTopics.FirstAsync()).Id;
    }
    internal static VoiceStartInput Input(Guid id, int topic) => new() { SessionId = id, TopicId = topic, OfferSdp = AiVoiceRealtimeProtocolTests.Sdp };
}

internal sealed class FakeVoiceProvider : IRealtimeVoiceClient
{
    public bool PauseCreate, FailAttach, FailHangup;
    public int Creates, Hangups;
    public TaskCompletionSource Dispatched = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Continue = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsConfigured => true;
    public async Task<VoiceCall> CreateCallAsync(string offer, string instructions, string safety, Func<string, Task> save, CancellationToken ct)
    {
        Interlocked.Increment(ref Creates); Dispatched.TrySetResult();
        if (PauseCreate) await Continue.Task;
        await save("rtc_fake_" + Guid.NewGuid().ToString("N")); return new(AiVoiceRealtimeProtocolTests.Sdp);
    }
    public Task<IVoiceSideband> AttachAsync(string id, string instructions, CancellationToken ct)
    { if (FailAttach) throw new VoiceException("provider_configuration_failed", 502); return Task.FromResult<IVoiceSideband>(new FakeSideband()); }
    public Task HangupAsync(string id, CancellationToken ct)
    { Interlocked.Increment(ref Hangups); if (FailHangup) throw new VoiceException("hangup_pending", 503); return Task.CompletedTask; }
}

internal sealed class FakeSideband : IVoiceSideband
{
    public readonly Channel<JsonElement> Events = Channel.CreateUnbounded<JsonElement>();
    public Task SendAsync(JsonElement message, CancellationToken ct) => Task.CompletedTask;
    public async Task<JsonElement> ReceiveAsync(CancellationToken ct) => await Events.Reader.ReadAsync(ct);
    public ValueTask DisposeAsync() { Events.Writer.TryComplete(); return ValueTask.CompletedTask; }
}
