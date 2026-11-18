using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Admin;
using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>Host-level checks for the admin content modules (vocabulary sets, listening lessons, payments, notifications).</summary>
public sealed class AdminContentPagesIntegrationTests
{
    [Fact]
    public async Task TheAdminVocabularyService_IsRegisteredInTheHost()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminVocabularyService>();

        Assert.IsType<AdminVocabularyService>(service);
    }
}
