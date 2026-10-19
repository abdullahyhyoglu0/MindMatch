using Microsoft.Extensions.Options;
using MindMatch.Api.Services;

namespace MindMatch.Test.Domain;

public sealed class TurnCredentialTests
{
    [Fact]
    public void TurnCredentials_AreScopedToUserAndShortLived()
    {
        var service = new TurnCredentialService(Options.Create(new TurnSettings
        {
            Urls = ["turn:turn.example.test:3478?transport=udp", "turns:turn.example.test:5349"],
            SharedSecret = "0123456789abcdef0123456789abcdef",
            CredentialLifetimeSeconds = 600
        }));
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var credentials = service.Create(userId);

        Assert.Equal(2, credentials.Urls.Count);
        Assert.Contains(userId.ToString("N"), credentials.Username);
        Assert.InRange(credentials.ExpiresAtUtc, before.AddSeconds(599), DateTime.UtcNow.AddSeconds(601));
        Assert.NotEqual("0123456789abcdef0123456789abcdef", credentials.Credential);
    }

    [Fact]
    public void TurnCredentials_AreUnavailableWithoutServerSecret()
    {
        var service = new TurnCredentialService(Options.Create(new TurnSettings
        {
            Urls = ["turn:turn.example.test"],
            SharedSecret = string.Empty
        }));
        Assert.Throws<InvalidOperationException>(() => service.Create(Guid.NewGuid()));
    }
}
