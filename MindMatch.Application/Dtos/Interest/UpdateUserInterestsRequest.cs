namespace MindMatch.Application.Dtos.Interest;

public sealed class UpdateUserInterestsRequest
{
    public List<Guid> InterestIds { get; set; } = [];
}
