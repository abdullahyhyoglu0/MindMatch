using MindMatch.Domain.Entities;
using MindMatch.Application.Dtos.Message;
using MindMatch.Application.Services;

namespace MindMatch.Test.Domain;

public sealed class PresenceAndLifecycleTests
{
    [Fact]
    public void StoryMedia_IsUnavailableAfterExpiryOrDeletion()
    {
        var userId = Guid.NewGuid();
        var story = new Story(userId, StoryMediaType.Image, Guid.NewGuid());
        var createdAt = story.CreatedAtUtc;

        Assert.True(story.IsAvailableAt(createdAt.AddHours(23)));
        Assert.False(story.IsAvailableAt(story.ExpiresAtUtc));

        story.Delete();
        Assert.False(story.IsAvailableAt(createdAt.AddHours(1)));
    }

    [Fact]
    public void PublicProfileProjection_HidesPrivacyControlsAndBirthDate()
    {
        var response = new MindMatch.Application.Dtos.UserProfile.UserProfileResponse
        {
            BirthDate = new DateOnly(1990, 1, 1),
            AllowMessageRequests = true,
            AllowCallsFromNonMatches = true,
            ShowOnlineStatus = true,
            ShowLastSeen = true,
            ShowReadReceipts = true,
            PremiumPriorityOptIn = true,
            ShowPremiumBadge = true
        };

        ProfilePrivacyMapper.ApplyPublicView(response);

        Assert.Null(response.BirthDate);
        Assert.False(response.AllowMessageRequests);
        Assert.False(response.AllowCallsFromNonMatches);
        Assert.False(response.ShowOnlineStatus);
        Assert.False(response.ShowLastSeen);
        Assert.False(response.ShowReadReceipts);
        Assert.False(response.PremiumPriorityOptIn);
        Assert.True(response.ShowPremiumBadge);
    }

    [Fact]
    public void Call_CannotBeAnsweredTwiceOrAfterMissed()
    {
        var call = new Call(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CallType.Video);
        call.Answer();
        Assert.Throws<InvalidOperationException>(call.Answer);
        Assert.Throws<InvalidOperationException>(call.MarkAsMissed);
        Assert.Equal(CallStatus.Answered, call.Status);
    }

    [Fact]
    public void Call_RingingCallCanBeMarkedMissedOnlyOnce()
    {
        var call = new Call(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CallType.Audio);
        call.MarkAsMissed();
        Assert.Equal(CallStatus.Missed, call.Status);
        Assert.Throws<InvalidOperationException>(call.MarkAsMissed);
    }

    [Fact]
    public void Story_UsesTwentyFourHourExpiry()
    {
        var before = DateTime.UtcNow;
        var story = new Story(Guid.NewGuid(), StoryMediaType.Image, Guid.NewGuid(), "caption");
        Assert.InRange(story.ExpiresAtUtc, before.AddHours(24), DateTime.UtcNow.AddHours(24).AddSeconds(1));
    }

    [Fact]
    public void PrivacyPreferences_AreStoredIndependently()
    {
        var profile = new UserProfile(Guid.NewGuid(), "Ada");
        profile.UpdatePreferences(true, false, false, true, false, true, false);
        Assert.True(profile.AllowMessageRequests);
        Assert.False(profile.AllowCallsFromNonMatches);
        Assert.False(profile.ShowOnlineStatus);
        Assert.True(profile.ShowLastSeen);
        Assert.False(profile.ShowReadReceipts);
    }

    [Fact]
    public void MessageReadState_IsIdempotentAndCannotRegress()
    {
        var message = new Message(Guid.NewGuid(), Guid.NewGuid(), MessageType.Text, "hello");
        message.MarkAsDelivered();
        message.MarkAsRead();
        var readAt = message.ReadAtUtc;
        message.MarkAsRead();
        message.MarkAsDelivered();
        Assert.Equal(MessageStatus.Read, message.Status);
        Assert.Equal(readAt, message.ReadAtUtc);
    }

    [Fact]
    public void RealtimeOutbox_RetriesAndClearsPayloadAfterDelivery()
    {
        var outbox = new RealtimeOutboxMessage(RealtimeDestinationType.User, Guid.NewGuid(),
            "NotificationReceived", "{\"title\":\"hello\"}", DateTime.UtcNow.AddDays(1));
        var originalPayload = outbox.PayloadJson;
        var now = DateTime.UtcNow;

        Assert.True(outbox.RecordFailure(now, "Temporary failure"));
        Assert.Equal(1, outbox.AttemptCount);
        Assert.True(outbox.NextAttemptAtUtc > now);
        Assert.True(outbox.MarkProcessed(DateTime.UtcNow));
        Assert.Equal("{}", outbox.PayloadJson);
        Assert.False(outbox.MarkProcessed(DateTime.UtcNow));
        Assert.NotEqual(originalPayload, outbox.PayloadJson);
    }

    [Fact]
    public void MessageRequest_OnlyReceiverCanAcceptAndItCannotBeAnsweredTwice()
    {
        var receiverId = Guid.NewGuid();
        var request = new MessageRequest(Guid.NewGuid(), Guid.NewGuid(), receiverId);
        Assert.Throws<UnauthorizedAccessException>(() => request.Accept(Guid.NewGuid()));
        request.Accept(receiverId);
        Assert.Equal(MessageRequestStatus.Accepted, request.Status);
        Assert.Equal(2, request.Version);
        Assert.Throws<InvalidOperationException>(() => request.Reject(receiverId));
    }

    [Fact]
    public void MessageRequest_AcceptedMatchUnlocksPreviouslyRejectedRequestConversation()
    {
        var request = new MessageRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        request.Reject(request.ReceiverId);

        request.MarkMatched();

        Assert.Equal(MessageRequestStatus.Accepted, request.Status);
        Assert.Equal(3, request.Version);
    }

    [Fact]
    public void RefreshToken_RevokeIsIdempotentAndMakesTokenInactive()
    {
        var token = new RefreshToken(Guid.NewGuid(), "sha256-hash", DateTime.UtcNow.AddDays(1));
        token.Revoke();
        var revokedAt = token.RevokedAtUtc;
        token.Revoke();
        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive);
        Assert.Equal(revokedAt, token.RevokedAtUtc);
        Assert.Equal(2, token.Version);
    }

    [Fact]
    public void PremiumEntitlement_StopsBeingActiveAtExpiry()
    {
        var start = DateTime.UtcNow.AddMinutes(-2);
        var expires = DateTime.UtcNow.AddMinutes(2);
        var entitlement = new PremiumEntitlement(Guid.NewGuid(), PremiumEntitlementSource.Promotion,
            "reference-1", start, expires);
        Assert.True(entitlement.IsActiveAt(DateTime.UtcNow));
        Assert.False(entitlement.IsActiveAt(expires));
        entitlement.Revoke();
        Assert.False(entitlement.IsActiveAt(DateTime.UtcNow));
    }

    [Fact]
    public void ReadReceiptIsHiddenFromSenderWhenRecipientDisabledIt()
    {
        var senderId = Guid.NewGuid();
        var response = new MessageResponse
        {
            SenderId = senderId,
            Status = MessageStatus.Read,
            ReadAtUtc = DateTime.UtcNow
        };

        MessagePrivacyMapper.ApplyReadReceiptPreference(response, senderId, recipientShowsReadReceipts: false);

        Assert.Equal(MessageStatus.Delivered, response.Status);
        Assert.Null(response.ReadAtUtc);
    }

    [Fact]
    public void ReadReceiptRemainsWhenRecipientAllowsIt()
    {
        var response = new MessageResponse
        {
            SenderId = Guid.NewGuid(),
            Status = MessageStatus.Read,
            ReadAtUtc = DateTime.UtcNow
        };
        var originalReadAt = response.ReadAtUtc;

        MessagePrivacyMapper.ApplyReadReceiptPreference(response, response.SenderId, recipientShowsReadReceipts: true);

        Assert.Equal(MessageStatus.Read, response.Status);
        Assert.Equal(originalReadAt, response.ReadAtUtc);
    }
}
