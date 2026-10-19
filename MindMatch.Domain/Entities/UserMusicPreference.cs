using MindMatch.Domain.Common;

namespace MindMatch.Domain.Entities;

public sealed class UserMusicPreference : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Genre { get; private set; } = string.Empty;
    public string NormalizedGenre { get; private set; } = string.Empty;

    private UserMusicPreference() { }

    public UserMusicPreference(Guid userId, string genre)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        UserId = userId;
        SetGenre(genre);
    }

    private void SetGenre(string genre)
    {
        if (string.IsNullOrWhiteSpace(genre)) throw new ArgumentException("Müzik tercihi boş olamaz.");
        genre = genre.Trim();
        if (genre.Length > 60) throw new ArgumentException("Müzik tercihi 60 karakteri aşamaz.");
        Genre = genre;
        NormalizedGenre = genre.ToUpperInvariant();
    }
}
