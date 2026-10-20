using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MindMatch.Application.Dtos.Calls;

namespace MindMatch.Api.Services;

public sealed class TurnCredentialService
{
    private const string CloudflareCredentialsUrl = "https://rtc.live.cloudflare.com/v1/turn/keys/{0}/credentials/generate-ice-servers";
    private readonly TurnSettings _settings;
    private readonly HttpClient _httpClient;

    public TurnCredentialService(IOptions<TurnSettings> settings, HttpClient httpClient)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
    }

    public async Task<TurnCredentialsResponse> CreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
        if (_settings.CredentialLifetimeSeconds is < 60 or > 86400)
            throw new InvalidOperationException("TURN kimlik bilgisi süresi 60-86400 saniye aralığında olmalıdır.");

        if (string.Equals(_settings.Provider, "Cloudflare", StringComparison.OrdinalIgnoreCase))
            return await CreateCloudflareAsync(cancellationToken);

        if (string.Equals(_settings.Provider, "Coturn", StringComparison.OrdinalIgnoreCase))
            return CreateCoturn(userId);

        throw new InvalidOperationException("TURN sağlayıcısı Cloudflare veya Coturn olmalıdır.");
    }

    private async Task<TurnCredentialsResponse> CreateCloudflareAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.CloudflareTurnKeyId) || string.IsNullOrWhiteSpace(_settings.CloudflareApiToken))
            throw new InvalidOperationException("Cloudflare TURN anahtarı API sunucusunda yapılandırılmamış.");

        using var request = new HttpRequestMessage(HttpMethod.Post,
            string.Format(CloudflareCredentialsUrl, Uri.EscapeDataString(_settings.CloudflareTurnKeyId)))
        {
            Content = JsonContent.Create(new { ttl = _settings.CredentialLifetimeSeconds })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.CloudflareApiToken);

        HttpResponseMessage response;
        try { response = await _httpClient.SendAsync(request, cancellationToken); }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Cloudflare TURN servisine ulaşılamadı.");
        }
        using (response)
        {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Cloudflare TURN kimlik bilgileri alınamadı. API token izinlerini ve TURN anahtarını kontrol edin.");

        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (!json.RootElement.TryGetProperty("iceServers", out var iceServers) || iceServers.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Cloudflare TURN yanıtında kullanılabilir relay sunucusu bulunamadı.");

            var turnServer = iceServers.EnumerateArray()
                .FirstOrDefault(server => server.TryGetProperty("urls", out var urls) && ContainsTurnUrl(urls));

            if (turnServer.ValueKind == JsonValueKind.Undefined ||
                !turnServer.TryGetProperty("urls", out var turnUrls) ||
                !turnServer.TryGetProperty("username", out var username) ||
                !turnServer.TryGetProperty("credential", out var credential))
                throw new InvalidOperationException("Cloudflare TURN yanıtında kullanılabilir relay sunucusu bulunamadı.");

            var urlsResult = turnUrls.ValueKind == JsonValueKind.Array
                ? turnUrls.EnumerateArray().Where(url => url.ValueKind == JsonValueKind.String)
                    .Select(url => url.GetString()!).Where(IsTurnUrl).ToArray()
                : turnUrls.ValueKind == JsonValueKind.String && IsTurnUrl(turnUrls.GetString())
                    ? [turnUrls.GetString()!]
                    : [];

            if (urlsResult.Length == 0 || string.IsNullOrWhiteSpace(username.GetString()) || string.IsNullOrWhiteSpace(credential.GetString()))
                throw new InvalidOperationException("Cloudflare TURN yanıtında geçerli kimlik bilgileri bulunamadı.");

            return new TurnCredentialsResponse(urlsResult, username.GetString()!, credential.GetString()!,
                DateTime.UtcNow.AddSeconds(_settings.CredentialLifetimeSeconds));
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Cloudflare TURN yanıtı okunamadı.");
        }
        }
    }

    private TurnCredentialsResponse CreateCoturn(Guid userId)
    {
        if (_settings.Urls.Length == 0 || _settings.Urls.Any(url => !IsTurnUrl(url)) ||
            Encoding.UTF8.GetByteCount(_settings.SharedSecret) < 32)
            throw new InvalidOperationException("Coturn ayarları API sunucusunda yapılandırılmamış.");

        var expiresAt = DateTime.UtcNow.AddSeconds(_settings.CredentialLifetimeSeconds);
        var username = $"{new DateTimeOffset(expiresAt).ToUnixTimeSeconds()}:{userId:N}";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_settings.SharedSecret));
        var credential = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(username)));
        return new TurnCredentialsResponse(_settings.Urls, username, credential, expiresAt);
    }

    private static bool ContainsTurnUrl(JsonElement urls) => urls.ValueKind switch
    {
        JsonValueKind.Array => urls.EnumerateArray().Any(url => url.ValueKind == JsonValueKind.String && IsTurnUrl(url.GetString())),
        JsonValueKind.String => IsTurnUrl(urls.GetString()),
        _ => false
    };

    private static bool IsTurnUrl(string? url) => !string.IsNullOrWhiteSpace(url) &&
        (url.StartsWith("turn:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("turns:", StringComparison.OrdinalIgnoreCase));
}
