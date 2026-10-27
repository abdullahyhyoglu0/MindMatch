namespace MindMatch.Application.Dtos.UserProfile;

public sealed class ProfilePreferencesRequest
{
    public bool AllowMessageRequests { get; set; }
    public bool AllowCallsFromNonMatches { get; set; }
    public bool ShowOnlineStatus { get; set; }
    public bool ShowLastSeen { get; set; }
    public bool ShowReadReceipts { get; set; }
    public bool ShowPremiumBadge { get; set; } = true;
    public bool PremiumPriorityOptIn { get; set; }
    public bool ShowFollowers { get; set; } = true;
    public bool ShowFollowing { get; set; } = true;
}
