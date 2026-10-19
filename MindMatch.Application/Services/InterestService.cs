
using MindMatch.Application.Dtos.Interest;
using MindMatch.Application.Interfaces;

namespace MindMatch.Application.Services;

public class InterestService : IInterestService
{
    private readonly IInterestRepository _interestRepository;

    public InterestService(IInterestRepository interestRepository)
    {
        _interestRepository = interestRepository;
    }

    public async Task<List<InterestResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var interests = await _interestRepository.GetAllAsync(
            cancellationToken);

        return interests.Select(interest => new InterestResponse
        {
            Id = interest.Id,
            Name = interest.Name
        }).ToList();
    }
}
