using MindMatch.Domain.Entities;

namespace MindMatch.Test.Domain;

public sealed class SafetyAndModerationTests
{
    [Fact]
    public void UserBlock_ShouldRejectSelfBlock()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new UserBlock(id, id));
    }

    [Fact]
    public void UserReport_ShouldCaptureSelectedMessageSnapshot()
    {
        var reporterId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var message = new Message(conversationId, reportedId, MessageType.Text, "evidence");
        var report = new UserReport(reporterId, reportedId, ReportReason.Harassment,
            "reason", conversationId);

        var snapshot = new ReportedMessage(report.Id, message);
        report.AddMessageSnapshot(snapshot);
        message.DeleteForEveryone(reportedId);

        Assert.Single(report.Messages);
        Assert.Equal("evidence", report.Messages.Single().ContentSnapshot);
    }

    [Fact]
    public void ReportedMessage_ClearsContentAndMediaReferencesAfterRetention()
    {
        var message = new Message(Guid.NewGuid(), Guid.NewGuid(), MessageType.Text, "evidence");
        var evidence = new ReportedMessage(Guid.NewGuid(), message);
        var expiry = evidence.EvidenceExpiresAtUtc;

        Assert.False(evidence.ClearExpiredEvidence(expiry.AddTicks(-1)));
        Assert.True(evidence.ClearExpiredEvidence(expiry));
        Assert.Null(evidence.MessageId);
        Assert.Null(evidence.ContentSnapshot);
        Assert.Null(evidence.MediaAssetIdSnapshot);
        Assert.Null(evidence.MediaReferenceSnapshot);
        Assert.False(evidence.ClearExpiredEvidence(expiry.AddDays(1)));
    }

    [Fact]
    public void ReportedMessage_MediaEvidenceExpiresAtRetentionBoundary()
    {
        var mediaAssetId = Guid.NewGuid();
        var message = new Message(Guid.NewGuid(), Guid.NewGuid(), MessageType.Image,
            mediaAssetId: mediaAssetId);
        var evidence = new ReportedMessage(Guid.NewGuid(), message);

        Assert.True(evidence.IsMediaEvidenceAvailableAt(mediaAssetId, evidence.EvidenceExpiresAtUtc.AddTicks(-1)));
        Assert.False(evidence.IsMediaEvidenceAvailableAt(mediaAssetId, evidence.EvidenceExpiresAtUtc));
        Assert.False(evidence.IsMediaEvidenceAvailableAt(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void PasswordResetToken_CanOnlyBeUsedOnce()
    {
        var token = new PasswordResetToken(Guid.NewGuid(), "token-hash", DateTime.UtcNow.AddMinutes(30));
        var now = DateTime.UtcNow;

        Assert.True(token.IsActive(now));
        token.MarkUsed(now);

        Assert.False(token.IsActive(now));
        Assert.Throws<InvalidOperationException>(() => token.MarkUsed(now.AddSeconds(1)));
    }

    [Fact]
    public void UserReport_ShouldOnlyAllowAssignedModeratorToResolve()
    {
        var report = new UserReport(Guid.NewGuid(), Guid.NewGuid(), ReportReason.Spam, null, null);
        var moderator = Guid.NewGuid();
        report.Assign(moderator);

        Assert.Throws<UnauthorizedAccessException>(() =>
            report.Resolve(Guid.NewGuid(), "resolved", true));

        report.Resolve(moderator, "resolved", true);
        Assert.Equal(ReportStatus.Resolved, report.Status);
    }

    [Fact]
    public void AdminFrozenAccount_CannotBeActivatedByOwner()
    {
        var user = new User("user@example.com", "hash");
        user.FreezeByAdmin(Guid.NewGuid(), "policy violation");

        Assert.Throws<InvalidOperationException>(() => user.Activate());
        Assert.False(user.IsActive);
    }

    [Fact]
    public void AdminFreeze_RejectsNonUtcOrPastExpiry()
    {
        var user = new User("user@test.invalid", "hash");
        var admin = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => user.FreezeByAdmin(admin, "reason", DateTime.Now.AddHours(1)));
        Assert.Throws<ArgumentException>(() => user.FreezeByAdmin(admin, "reason", DateTime.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public void TemporaryAdminFreeze_ExpiresAndInvalidatesSessions()
    {
        var user = new User("user@test.invalid", "hash");
        var adminId = Guid.NewGuid();
        var expiry = DateTime.UtcNow.AddMinutes(1);
        user.FreezeByAdmin(adminId, "temporary review", expiry);
        var sessionVersion = user.SessionVersion;

        Assert.False(user.ExpireAdminFreeze(expiry.AddTicks(-1)));
        Assert.True(user.ExpireAdminFreeze(expiry));
        Assert.True(user.IsActive);
        Assert.False(user.IsFrozenByAdmin);
        Assert.Equal(sessionVersion + 1, user.SessionVersion);
        Assert.False(user.ExpireAdminFreeze(expiry.AddDays(1)));
    }

    [Fact]
    public void DiscoveryDecision_RejectRemainsRejectedWhenSeenAgain()
    {
        var decision = new DiscoveryDecision(Guid.NewGuid(), Guid.NewGuid());
        decision.Reject();
        var rejectedAt = decision.RejectedAtUtc;

        decision.MarkSeen();

        Assert.Equal(rejectedAt, decision.RejectedAtUtc);
        Assert.True(decision.SeenAtUtc >= rejectedAt);
    }

    [Fact]
    public void DiscoveryDecision_CannotTargetSelf()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new DiscoveryDecision(userId, userId));
    }

    [Fact]
    public void MusicPreference_NormalizesGenreForMatching()
    {
        var preference = new UserMusicPreference(Guid.NewGuid(), "  Alternative Rock ");
        Assert.Equal("Alternative Rock", preference.Genre);
        Assert.Equal("ALTERNATIVE ROCK", preference.NormalizedGenre);
    }

    [Fact]
    public void MusicPreference_RejectsUnboundedGenre()
    {
        Assert.Throws<ArgumentException>(() => new UserMusicPreference(Guid.NewGuid(), new string('x', 61)));
    }

    [Fact]
    public void UserProfile_CreatesStableUniqueFallbackNickname()
    {
        var userId = Guid.NewGuid();
        var profile = new UserProfile(userId, "Display Name");
        Assert.Equal($"user{userId:N}"[..30], profile.Nickname);
        Assert.Equal(profile.Nickname.ToUpperInvariant(), profile.NormalizedNickname);
    }

    [Fact]
    public void UserProfile_RejectsInvalidNicknameCharacters()
    {
        var profile = new UserProfile(Guid.NewGuid(), "Display Name");
        Assert.Throws<ArgumentException>(() => profile.ChangeNickname("bad handle!"));
    }
}
