using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Services;

public sealed class ActiveUserHubFilter(IUserRepository users) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var principal = invocationContext.Context.User;
        var userValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        var versionValue = principal?.FindFirstValue("tv");
        if (!Guid.TryParse(userValue, out var userId) || !int.TryParse(versionValue, out var tokenVersion))
            return await Reject(invocationContext.Context);

        var user = await users.GetByIdAsync(userId, invocationContext.Context.ConnectionAborted);
        if (user is null || !user.IsActive || user.SessionVersion != tokenVersion)
            return await Reject(invocationContext.Context);

        return await next(invocationContext);
    }

    private static ValueTask<object?> Reject(HubCallerContext context)
    {
        context.Abort();
        throw new HubException("Oturum geçersiz veya hesap etkin değil.");
    }
}
