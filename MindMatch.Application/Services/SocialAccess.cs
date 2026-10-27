using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

internal static class SocialAccess
{
    public static async Task<bool> CanViewProfileAsync(Guid viewerId, UserProfile profile,
        IUserFollowRepository follows, CancellationToken cancellationToken)
    {
        if (viewerId == profile.UserId || profile.IsProfileVisible)
            return true;
        return await follows.IsAcceptedAsync(viewerId, profile.UserId, cancellationToken);
    }

    public static DiscoverMessagingMode ResolveMessaging(UserProfile target, bool canViewProfile,
        bool hasOpenConversation)
    {
        if (hasOpenConversation) return DiscoverMessagingMode.Direct;
        if (!canViewProfile) return DiscoverMessagingMode.Unavailable;
        if (!target.IsProfileVisible) return DiscoverMessagingMode.Direct;
        return target.AllowMessageRequests ? DiscoverMessagingMode.Request : DiscoverMessagingMode.Direct;
    }
}
