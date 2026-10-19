
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.UserProfile;

public class UserProfileResponse
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public DateOnly? BirthDate { get; set; }

    public Gender Gender { get; set; }

    public GenderPreference GenderPreference { get; set; }

    public int? MinimumMatchAge { get; set; }

    public int? MaximumMatchAge { get; set; }

    public string? City { get; set; }

    public bool IsProfileVisible { get; set; }
    public bool AllowMessageRequests { get; set; }
    public bool AllowCallsFromNonMatches { get; set; }
    public bool ShowOnlineStatus { get; set; }
    public bool ShowLastSeen { get; set; }
    public bool ShowReadReceipts { get; set; }
    public bool ShowPremiumBadge { get; set; }
    public bool PremiumPriorityOptIn { get; set; }
    public int? Age { get; set; }

    public List<Guid> InterestIds { get; set; } = [];
    public List<string> DiscoveryReasons { get; set; } = [];
}
