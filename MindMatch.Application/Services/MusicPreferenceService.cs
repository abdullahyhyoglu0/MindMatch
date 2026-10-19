using MindMatch.Application.Dtos.UserProfile;
using MindMatch.Application.Interfaces;

namespace MindMatch.Application.Services;

public sealed class MusicPreferenceService
{
    private readonly IUserMusicPreferenceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    public MusicPreferenceService(IUserMusicPreferenceRepository repository, IUnitOfWork unitOfWork)
    { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<List<string>> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        (await _repository.GetByUserIdAsync(userId, cancellationToken)).Select(x => x.Genre).ToList();

    public async Task UpdateAsync(Guid userId, MusicPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        ArgumentNullException.ThrowIfNull(request);
        if (request.Genres is null || request.Genres.Count > 20)
            throw new ArgumentException("En fazla 20 müzik tercihi belirtebilirsiniz.");
        var normalized = request.Genres.Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (normalized.Any(x => x.Length > 60)) throw new ArgumentException("Müzik tercihi 60 karakteri aşamaz.");
        await _repository.ReplaceAsync(userId, normalized, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
