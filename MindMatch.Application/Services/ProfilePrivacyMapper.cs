using MindMatch.Application.Dtos.UserProfile;

namespace MindMatch.Application.Services;

public static class ProfilePrivacyMapper
{
    public static void ApplyPublicView(UserProfileResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.BirthDate = null;
        response.AllowMessageRequests = false;
        response.AllowCallsFromNonMatches = false;
        response.ShowOnlineStatus = false;
        response.ShowLastSeen = false;
        response.ShowReadReceipts = false;
        response.PremiumPriorityOptIn = false;
    }
}
