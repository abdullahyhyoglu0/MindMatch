using MindMatch.Application.Dtos.Auth;
using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Login_WithInvalidPassword_IsRejected()
    {
        var user = new User("person@test.invalid", "stored-hash");
        var fixture = new Fixture(user);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LoginAsync(
            new LoginRequest { Email = user.Email, Password = "wrong" }));
        Assert.Empty(fixture.RefreshTokens.Tokens);
    }

    [Fact]
    public async Task Login_WithAdminFrozenAccount_IsRejected()
    {
        var user = new User("person@test.invalid", "stored-hash");
        user.FreezeByAdmin(Guid.NewGuid(), "safety review");
        var fixture = new Fixture(user, passwordMatches: true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LoginAsync(
            new LoginRequest { Email = user.Email, Password = "correct" }));
        Assert.Empty(fixture.RefreshTokens.Tokens);
    }

    [Fact]
    public async Task RefreshToken_RotatesTokenAndRevokesPreviousToken()
    {
        var user = new User("person@test.invalid", "stored-hash");
        var fixture = new Fixture(user);
        var oldToken = new RefreshToken(user.Id, RefreshTokenHasher.Hash("old-refresh"), DateTime.UtcNow.AddDays(1));
        fixture.RefreshTokens.Tokens.Add(oldToken);

        var response = await fixture.Service.RefreshTokenAsync("old-refresh");

        Assert.True(oldToken.IsRevoked);
        Assert.NotEqual("old-refresh", response.RefreshToken);
        Assert.Equal(2, fixture.RefreshTokens.Tokens.Count);
        Assert.Contains(fixture.RefreshTokens.Tokens, token => token.IsActive &&
            token.TokenHash == RefreshTokenHasher.Hash(response.RefreshToken));
    }

    [Fact]
    public async Task RefreshToken_RejectsRevokedToken()
    {
        var user = new User("person@test.invalid", "stored-hash");
        var fixture = new Fixture(user);
        var revoked = new RefreshToken(user.Id, RefreshTokenHasher.Hash("revoked-refresh"), DateTime.UtcNow.AddDays(1));
        revoked.Revoke();
        fixture.RefreshTokens.Tokens.Add(revoked);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.RefreshTokenAsync("revoked-refresh"));
    }

    private sealed class Fixture
    {
        public TokenRepo RefreshTokens { get; } = new();
        public AuthService Service { get; }

        public Fixture(User user, bool passwordMatches = false)
        {
            Service = new AuthService(new UserRepo(user), new ProfileRepo(), RefreshTokens,
                new PasswordHasher(passwordMatches), new Tokens(), new ResetTokens(), new EmailSender());
        }
    }

    private sealed class UserRepo(User user) : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult<User?>(user.Email == email ? user : null);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(user.Id == id ? user : null);
        public Task AddAsync(User item, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => Task.FromResult(user.Email == email);
        public Task<List<User>> GetExpiredAdminFrozenBatchAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult(new List<User>());
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ProfileRepo : IUserProfileRepository
    {
        public Task<UserProfile?> GetByUserIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<UserProfile?>(null);
        public Task AddAsync(UserProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> IsNicknameTakenAsync(string nickname, Guid? exceptUserId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<List<UserProfile>> DiscoverAsync(Guid viewerId, Gender viewerGender, GenderPreference preference, int? age, int? minAge, int? maxAge, string? city, IReadOnlyCollection<Guid> excluded, bool premiumOnly, int skip, int take, CancellationToken ct = default) => Task.FromResult(new List<UserProfile>());
        public Task<List<Guid>> GetRecentlySeenOrRejectedUserIdsAsync(Guid id, DateTime since, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        public Task RecordDiscoveryDecisionAsync(Guid viewerId, Guid targetId, bool rejected, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TokenRepo : IRefreshTokenRepository
    {
        public List<RefreshToken> Tokens { get; } = [];
        public Task<RefreshToken?> GetByTokenHashAsync(string hash, CancellationToken ct = default) => Task.FromResult(Tokens.FirstOrDefault(x => x.TokenHash == hash));
        public Task AddAsync(RefreshToken token, CancellationToken ct = default) { Tokens.Add(token); return Task.CompletedTask; }
        public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default) { foreach (var token in Tokens.Where(x => x.UserId == userId)) token.Revoke(); return Task.CompletedTask; }
        public Task<int> DeleteInactiveBatchAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class PasswordHasher(bool matches) : IPasswordHasher
    {
        public string HashPassword(string password) => "new-hash";
        public bool VerifyPassword(string password, string hash) => matches;
    }

    private sealed class Tokens : ITokenService
    {
        private int _refreshSequence;
        public string GenerateAccessToken(Guid id, string email, UserRole role = UserRole.User, int sessionVersion = 0) => "access";
        public string GenerateRefreshToken() => $"refresh-{Interlocked.Increment(ref _refreshSequence)}";
        public DateTime GetAccessTokenExpiryUtc() => DateTime.UtcNow.AddMinutes(15);
        public DateTime GetRefreshTokenExpiryUtc() => DateTime.UtcNow.AddDays(30);
    }

    private sealed class ResetTokens : IPasswordResetTokenRepository
    {
        public Task AddAsync(PasswordResetToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PasswordResetToken?> GetByHashAsync(string hash, CancellationToken ct = default) => Task.FromResult<PasswordResetToken?>(null);
        public Task RevokeActiveForUserAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> DeleteInactiveBatchAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class EmailSender : IPasswordResetEmailSender
    {
        public Task<bool> SendAsync(string email, string token, CancellationToken ct = default) => Task.FromResult(true);
    }
}
