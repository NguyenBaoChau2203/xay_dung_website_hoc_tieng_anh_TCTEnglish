using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiVoiceInlineRenderTests
{
    [Theory]
    [InlineData("/AI/Chat")]
    [InlineData("/AI/Chat?embed=true")]
    public async Task ChatRendersBothModesWithoutVoiceNavigationAndKeepsTextContracts(string path)
    {
        await using var f = AiVoiceSessionLifecycleTests.VoiceFactory(new()); await f.InitializeAsync();
        using var client = AiVoiceApiSecurityTests.Client(f, TestDataIds.AdminUserId);
        var html = await client.GetStringAsync(path);
        Assert.Contains("data-ai-mode=\"voice\"", html);
        Assert.Contains("data-ai-mode=\"text\"", html);
        Assert.Contains("id=\"aiVoiceMode\"", html);
        Assert.Contains("id=\"aiTextMode\"", html);
        Assert.DoesNotContain("href=\"/AI/Voice\"", html);
        foreach (var id in new[] { "chatWindow", "chatForm", "messageInput", "sendBtn", "stopBtn", "conversationIdInput", "aiVoicePage", "voiceStart", "voiceStop", "voiceTopicModal" })
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "id=\"" + id + "\"").Cast<System.Text.RegularExpressions.Match>());
        Assert.Contains("/js/ai-chat-mode.js", html); Assert.Contains("/js/ai-voice-page.js", html);
        Assert.Contains("__RequestVerificationToken", html);
    }
}
