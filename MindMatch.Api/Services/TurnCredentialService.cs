using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MindMatch.Application.Dtos.Calls;

namespace MindMatch.Api.Services;

public sealed class TurnCredentialService
{
    private readonly TurnSettings _settings;
    public TurnCredentialService(IOptions<TurnSettings> settings) => _settings = settings.Value;

    public TurnCredentialsResponse Create(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        if (_settings.Urls.Length == 0 || _settings.Urls.Any(url =>
                string.IsNullOrWhiteSpace(url) || !(url.StartsWith("turn:", StringComparison.OrdinalIgnoreCase) ||
                                                    url.StartsWith("turns:", StringComparison.OrdinalIgnoreCase))) ||
            Encoding.UTF8.GetByteCount(_settings.SharedSecret) < 32)
            throw new InvalidOperationException("TURN servisi sunucu tarafında yapılandırılmamış.");
        if (_settings.CredentialLifetimeSeconds is < 60 or > 3600)
            throw new InvalidOperationException("TURN kimlik bilgisi süresi 60-3600 saniye aralığında olmalıdır.");

        var expiresAt = DateTime.UtcNow.AddSeconds(_settings.CredentialLifetimeSeconds);
        var username = $"{new DateTimeOffset(expiresAt).ToUnixTimeSeconds()}:{userId:N}";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_settings.SharedSecret));
        var credential = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(username)));
        return new TurnCredentialsResponse(_settings.Urls, username, credential, expiresAt);
    }
}
