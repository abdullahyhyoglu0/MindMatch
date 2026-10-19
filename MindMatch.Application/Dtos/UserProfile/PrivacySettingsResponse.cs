namespace MindMatch.Application.Dtos.UserProfile;

public sealed record PrivacySettingsResponse(
    bool ShowReadReceipts,
    bool ShowLastSeen,
    bool ShowOnlineStatus,
    bool AllowMessageRequests,
    bool AllowCallsFromNonMatches,
    bool ShowPremiumBadge,
    bool PremiumPriorityOptIn);
