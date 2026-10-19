namespace MindMatch.Application.Dtos.Premium;

public sealed class PremiumStatusResponse
{
    public bool IsPremium { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
}
