using Microsoft.AspNetCore.SignalR;
using TCTEnglish.Services.Accounts;
using TCTVocabulary.Models;

namespace TCTEnglish.Hubs;

public sealed class AccountSecurityHubFilter(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocation,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        if (!await AccountSessionValidator.IsValidAsync(context, invocation.Context.User,
                timeProvider.GetUtcNow().UtcDateTime, invocation.Context.ConnectionAborted))
        {
            invocation.Context.Abort();
            throw new HubException("Phiên đăng nhập đã thay đổi. Hãy đăng nhập lại.");
        }
        return await next(invocation);
    }
}
