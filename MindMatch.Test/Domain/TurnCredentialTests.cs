using Microsoft.Extensions.Options;
using MindMatch.Api.Services;

namespace MindMatch.Test.Domain;

public sealed class TurnCredentialTests
{
    [Fact]
    public async Task TurnCredentials_AreScopedToUserAndShortLived()
    {
        using var httpClient = new HttpClient();
        var service = new TurnCredentialService(Options.Create(new TurnSettings
        {
            Provider = "Coturn",
            Urls = ["turn:turn.example.test:3478?transport=udp", "turns:turn.example.test:5349"],
            SharedSecret = "0123456789abcdef0123456789abcdef",
            CredentialLifetimeSeconds = 600
        }), httpClient);
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var credentials = await service.CreateAsync(userId, CancellationToken.None);

        Assert.Equal(2, credentials.Urls.Count);
        Assert.Contains(userId.ToString("N"), credentials.Username);
        Assert.InRange(credentials.ExpiresAtUtc, before.AddSeconds(599), DateTime.UtcNow.AddSeconds(601));
        Assert.NotEqual("0123456789abcdef0123456789abcdef", credentials.Credential);
    }

    [Fact]
    public async Task TurnCredentials_AreUnavailableWithoutServerSecret()
    {
        using var httpClient = new HttpClient();
        var service = new TurnCredentialService(Options.Create(new TurnSettings
        {
            Provider = "Coturn",
            Urls = ["turn:turn.example.test"],
            SharedSecret = string.Empty
        }), httpClient);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
