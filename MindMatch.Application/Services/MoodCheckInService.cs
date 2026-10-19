
using MindMatch.Application.Dtos.MoodCheckIn;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public class MoodCheckInService : IMoodCheckInService
{
    private readonly IMoodCheckInRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public MoodCheckInService(
        IMoodCheckInRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MoodCheckInResponse> CreateAsync(
        Guid userId,
        CreateMoodCheckInRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimliği geçersiz.");

        ArgumentNullException.ThrowIfNull(request);

        var moodCheckIn = new MoodCheckIn(
            userId,
            request.Mood,
            request.Energy,
            request.DesiredConversation);

        await _repository.AddAsync(
            moodCheckIn, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(moodCheckIn);
    }

    public async Task<MoodCheckInResponse?> GetMyLatestAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Kullanıcı kimliği geçersiz.");

        var moodCheckIn = await _repository.GetLatestByUserIdAsync(
            userId, cancellationToken);

        if (moodCheckIn is null)
            return null;

        return MapToResponse(moodCheckIn);
    }

    private static MoodCheckInResponse MapToResponse(
        MoodCheckIn moodCheckIn)
    {
        return new MoodCheckInResponse
        {
            Id = moodCheckIn.Id,
            Mood = moodCheckIn.Mood,
            Energy = moodCheckIn.Energy,
            DesiredConversation = moodCheckIn.DesiredConversation,
            ExpiresAtUtc = moodCheckIn.ExpiresAtUtc,
            IsExpired = moodCheckIn.IsExpired()
        };
    }
}
