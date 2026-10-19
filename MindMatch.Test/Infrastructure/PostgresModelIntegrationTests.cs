using Microsoft.EntityFrameworkCore;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Test.Infrastructure;

[CollectionDefinition("PostgreSQL integration", DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINDMATCH_TEST_POSTGRES_CONNECTION")))
            Skip = "MINDMATCH_TEST_POSTGRES_CONNECTION is not configured.";
    }
}

/// <summary>
/// Runs against a disposable PostgreSQL database when MINDMATCH_TEST_POSTGRES_CONNECTION is set.
/// The suite intentionally creates the schema from the EF model and does not run migrations.
/// </summary>
[Collection("PostgreSQL integration")]
public sealed class PostgresModelIntegrationTests
{
    [PostgreSqlFact]
    public async Task PostgreSql_FirstMessageForPendingRequestSkipsExpiredAndDeletedContent()
    {
        var connectionString = Environment.GetEnvironmentVariable("MINDMATCH_TEST_POSTGRES_CONNECTION");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        EnsureIsDisposableDatabase(connectionString);

        var options = new DbContextOptionsBuilder<MindMatchDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new MindMatchDbContext(options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var sender = new User("request-sender@test.invalid", "hash");
        var receiver = new User("request-receiver@test.invalid", "hash");
        var conversation = new Conversation(sender.Id, receiver.Id);
        var expired = new Message(conversation.Id, sender.Id, MessageType.Text, "expired initial message");
        var deleted = new Message(conversation.Id, sender.Id, MessageType.Text, "deleted initial message");
        deleted.DeleteForEveryone(sender.Id);
        var current = new Message(conversation.Id, sender.Id, MessageType.Text, "current initial message");
        context.Users.AddRange(sender, receiver);
        context.Conversations.Add(conversation);
        context.Messages.AddRange(expired, deleted, current);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE messages SET "ExpiresAtUtc" = {DateTime.UtcNow.AddMinutes(-1)}
            WHERE "Id" = {expired.Id}
            """);
        context.ChangeTracker.Clear();

        var repository = new MindMatch.Infrastructure.Repositories.MessageRepository(context);
        var result = await repository.GetFirstBySenderAsync(conversation.Id, sender.Id);

        Assert.NotNull(result);
        Assert.Equal(current.Id, result.Id);
        Assert.Equal("current initial message", result.Content);
    }

    [PostgreSqlFact]
    public async Task PostgreSql_EnforcesForeignKeysUniqueIndexesAndCascadeRules()
    {
        var connectionString = Environment.GetEnvironmentVariable("MINDMATCH_TEST_POSTGRES_CONNECTION");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        EnsureIsDisposableDatabase(connectionString);

        var options = new DbContextOptionsBuilder<MindMatchDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new MindMatchDbContext(options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        var user1 = new User("integration-one@test.invalid", "hash");
        var user2 = new User("integration-two@test.invalid", "hash");
        user2.ChangeRole(UserRole.Admin);
        user1.FreezeByAdmin(user2.Id, "temporary integration suspension", DateTime.UtcNow.AddMinutes(5));
        context.Users.AddRange(user1, user2);
        var profile1 = new UserProfile(user1.Id, "Integration One", "integ_one");
        var profile2 = new UserProfile(user2.Id, "Integration Two", "integ_two");
        profile2.SetProfileVisibility(true);
        context.UserProfiles.AddRange(profile1, profile2);
        await context.SaveChangesAsync();

        await using (var blockContext1 = new MindMatchDbContext(options))
        await using (var blockContext2 = new MindMatchDbContext(options))
        {
            var blockRepository1 = new MindMatch.Infrastructure.Repositories.UserBlockRepository(blockContext1);
            var blockRepository2 = new MindMatch.Infrastructure.Repositories.UserBlockRepository(blockContext2);
            await Task.WhenAll(
                blockRepository1.AddAsync(new UserBlock(user1.Id, user2.Id)),
                blockRepository2.AddAsync(new UserBlock(user1.Id, user2.Id)));
            await Task.WhenAll(blockContext1.SaveChangesAsync(), blockContext2.SaveChangesAsync());
        }
        Assert.Equal(1, await context.UserBlocks.CountAsync(x =>
            x.BlockerId == user1.Id && x.BlockedUserId == user2.Id));

        var profileRepository = new MindMatch.Infrastructure.Repositories.UserProfileRepository(context);
        var blockRepository = new MindMatch.Infrastructure.Repositories.UserBlockRepository(context);
        Assert.Single(await profileRepository.DiscoverAsync(user1.Id, Gender.PreferNotToSay,
            GenderPreference.Everyone, null, null, null, null, [], false, 0, 10));
        var blockedIds = await blockRepository.GetRelatedUserIdsAsync(user1.Id);
        Assert.Empty(await profileRepository.DiscoverAsync(user1.Id, Gender.PreferNotToSay,
            GenderPreference.Everyone, null, null, null, null, blockedIds, false, 0, 10));

        var viewedStory = new Story(user2.Id, StoryMediaType.Image, "integration-story-key");
        context.Stories.Add(viewedStory);
        await context.SaveChangesAsync();
        context.StoryViews.Add(new StoryView(viewedStory.Id, user1.Id));
        await context.SaveChangesAsync();
        var viewedStoryIds = await new MindMatch.Infrastructure.Repositories.StoryRepository(context)
            .GetViewedStoryIdsAsync(user1.Id, [viewedStory.Id, Guid.NewGuid()]);
        Assert.Equal([viewedStory.Id], viewedStoryIds);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE users SET "AdminFrozenUntilUtc" = {DateTime.UtcNow.AddMinutes(-1)}
            WHERE "Id" = {user1.Id}
            """);
        context.ChangeTracker.Clear();
        var expiredUsers = await new MindMatch.Infrastructure.Repositories.UserRepository(context)
            .GetExpiredAdminFrozenBatchAsync(DateTime.UtcNow, 10);
        var expiredUser = Assert.Single(expiredUsers);
        Assert.True(expiredUser.ExpireAdminFreeze(DateTime.UtcNow));
        await context.SaveChangesAsync();
        Assert.True(expiredUser.IsActive);

        var photoAssets = Enumerable.Range(0, 3).Select(_ => new MediaAsset(user1.Id,
            MediaCategory.ProfilePhoto, $"profilephoto/{Guid.NewGuid():N}.png", "image/png", 128, 1, 1)).ToArray();
        context.MediaAssets.AddRange(photoAssets);
        await context.SaveChangesAsync();
        var primaryPhoto = new ProfilePhoto(user1.Id, photoAssets[0].Id, 0, true);
        var secondaryPhoto = new ProfilePhoto(user1.Id, photoAssets[1].Id, 1);
        context.ProfilePhotos.AddRange(primaryPhoto, secondaryPhoto);
        await context.SaveChangesAsync();
        context.ProfilePhotos.Add(new ProfilePhoto(user1.Id, photoAssets[2].Id, 2, true));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.ProfilePhotos.CountAsync(x => x.UserId == user1.Id && x.IsPrimary));
        await new MindMatch.Infrastructure.Repositories.ProfilePhotoRepository(context)
            .SetPrimaryForUserAsync(user1.Id, secondaryPhoto.Id);
        Assert.False(await context.ProfilePhotos.AnyAsync(x => x.Id == primaryPhoto.Id && x.IsPrimary));
        Assert.True(await context.ProfilePhotos.AnyAsync(x => x.Id == secondaryPhoto.Id && x.IsPrimary));

        var conversation = new Conversation(user1.Id, user2.Id);
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync();

        var message = new Message(conversation.Id, user1.Id, MessageType.Text, "integration message");
        context.Messages.Add(message);
        await context.SaveChangesAsync();

        var messageAsset = new MediaAsset(user1.Id, MediaCategory.MessageImage,
            $"message/{Guid.NewGuid():N}.jpg", "image/jpeg", 10);
        context.MediaAssets.Add(messageAsset);
        await context.SaveChangesAsync();
        messageAsset.BindToConversation(conversation.Id);
        var mediaMessage = new Message(conversation.Id, user1.Id, MessageType.Image,
            mediaAssetId: messageAsset.Id);
        context.Messages.Add(mediaMessage);
        await context.SaveChangesAsync();
        var mediaRepository = new MindMatch.Infrastructure.Repositories.MediaAssetRepository(context);
        Assert.True(await mediaRepository.IsReferencedAsync(messageAsset.Id));
        var evidenceReport = new UserReport(user2.Id, user1.Id, ReportReason.InappropriateMedia,
            "integration evidence", conversation.Id);
        evidenceReport.AddMessageSnapshot(new ReportedMessage(evidenceReport.Id, mediaMessage));
        context.UserReports.Add(evidenceReport);
        context.ModerationAudits.Add(new ModerationAudit(user2.Id,
            ModerationActionType.ReportEvidenceViewed, "Evidence viewed in integration test.",
            user1.Id, evidenceReport.Id));
        await context.SaveChangesAsync();
        mediaMessage.DeleteForEveryone(user1.Id);
        await context.SaveChangesAsync();
        Assert.True(await mediaRepository.IsReferencedAsync(messageAsset.Id));
        messageAsset.MarkDeleted();
        await context.SaveChangesAsync();
        Assert.DoesNotContain(await mediaRepository.GetDeletedUnreferencedBatchAsync(50),
            asset => asset.Id == messageAsset.Id);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE reported_messages SET "EvidenceExpiresAtUtc" = {DateTime.UtcNow.AddMinutes(-1)}
            WHERE "ReportId" = {evidenceReport.Id}
            """);
        context.ChangeTracker.Clear();
        var reportRepository = new MindMatch.Infrastructure.Repositories.ReportRepository(context);
        Assert.Equal(1, await reportRepository.ClearExpiredEvidenceBatchAsync(DateTime.UtcNow, 10));
        await context.SaveChangesAsync();
        var storedReport = await reportRepository.GetByIdAsync(evidenceReport.Id);
        var clearedEvidence = Assert.Single(storedReport!.Messages);
        Assert.Null(clearedEvidence.MediaAssetIdSnapshot);
        Assert.Null(clearedEvidence.ContentSnapshot);
        var storedMediaAsset = await context.MediaAssets.SingleAsync(x => x.Id == messageAsset.Id);
        Assert.False(await mediaRepository.IsReferencedAsync(storedMediaAsset.Id));
        storedMediaAsset.MarkDeleted();
        await context.SaveChangesAsync();
        Assert.Contains(await mediaRepository.GetDeletedUnreferencedBatchAsync(50),
            asset => asset.Id == storedMediaAsset.Id);
        Assert.Contains(await context.ModerationAudits.ToListAsync(), x =>
            x.ReportId == evidenceReport.Id && x.Action == ModerationActionType.ReportEvidenceViewed);

        context.MessageDeletions.Add(new MessageDeletion(message.Id, user1.Id));
        await context.SaveChangesAsync();

        var request = new MessageRequest(conversation.Id, user1.Id, user2.Id);
        context.MessageRequests.Add(request);
        await context.SaveChangesAsync();
        var pendingMatch = new Match(user1.Id, user2.Id);
        context.Matches.Add(pendingMatch);
        await context.SaveChangesAsync();
        var messageRequestRepository = new MindMatch.Infrastructure.Repositories.MessageRequestRepository(context);
        Assert.Empty(await messageRequestRepository.GetIncomingAsync(user2.Id));
        var matchRepository = new MindMatch.Infrastructure.Repositories.MatchRepository(context);
        Assert.Empty(await matchRepository.GetIncomingPendingAsync(user2.Id));

        var user3 = new User("integration-three@test.invalid", "hash");
        var unblockedConversation = new Conversation(user2.Id, user3.Id);
        context.Users.Add(user3);
        context.Conversations.Add(unblockedConversation);
        await context.SaveChangesAsync();
        context.MessageRequests.Add(new MessageRequest(unblockedConversation.Id, user3.Id, user2.Id));
        context.Matches.Add(new Match(user3.Id, user2.Id));
        await context.SaveChangesAsync();
        Assert.Single(await messageRequestRepository.GetIncomingAsync(user2.Id));
        Assert.Single(await matchRepository.GetIncomingPendingAsync(user2.Id));

        var conversationRepository = new MindMatch.Infrastructure.Repositories.ConversationRepository(context);
        Assert.DoesNotContain(await conversationRepository.GetByUserIdAsync(user1.Id), x => x.Id == conversation.Id);
        request.Accept(user2.Id);
        await context.SaveChangesAsync();
        Assert.Contains(await conversationRepository.GetByUserIdAsync(user1.Id), x => x.Id == conversation.Id);

        context.MessageDeletions.Add(new MessageDeletion(message.Id, user1.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var duplicatePair = new Conversation(user2.Id, user1.Id);
        context.Conversations.Add(duplicatePair);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var orphanMessage = new Message(Guid.NewGuid(), user1.Id, MessageType.Text, "invalid FK");
        context.Messages.Add(orphanMessage);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        await context.Messages.Where(x => x.Id == message.Id).ExecuteDeleteAsync();
        Assert.False(await context.MessageDeletions.AnyAsync(x => x.MessageId == message.Id));
    }

    [PostgreSqlFact]
    public async Task PostgreSql_OptimisticConcurrencyRejectsStaleStateTransitions()
    {
        var connectionString = Environment.GetEnvironmentVariable("MINDMATCH_TEST_POSTGRES_CONNECTION");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        EnsureIsDisposableDatabase(connectionString);

        var options = new DbContextOptionsBuilder<MindMatchDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var setup = new MindMatchDbContext(options);
        await setup.Database.EnsureDeletedAsync();
        await setup.Database.EnsureCreatedAsync();
        var requester = new User("concurrency-one@test.invalid", "hash");
        var receiver = new User("concurrency-two@test.invalid", "hash");
        setup.Users.AddRange(requester, receiver);
        var match = new Match(requester.Id, receiver.Id);
        setup.Matches.Add(match);
        var mediaConversation = new Conversation(requester.Id, receiver.Id);
        var otherUser = new User("concurrency-three@test.invalid", "hash");
        setup.Users.Add(otherUser);
        var otherConversation = new Conversation(requester.Id, otherUser.Id);
        setup.Conversations.AddRange(mediaConversation, otherConversation);
        var messageMedia = new MediaAsset(requester.Id, MediaCategory.MessageImage,
            $"message/{Guid.NewGuid():N}.jpg", "image/jpeg", 10);
        setup.MediaAssets.Add(messageMedia);
        await setup.SaveChangesAsync();

        await using var first = new MindMatchDbContext(options);
        await using var second = new MindMatchDbContext(options);
        var firstCopy = await first.Matches.SingleAsync(x => x.Id == match.Id);
        var secondCopy = await second.Matches.SingleAsync(x => x.Id == match.Id);
        firstCopy.Accept(receiver.Id);
        await first.SaveChangesAsync();
        secondCopy.Reject(receiver.Id);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        setup.Matches.Add(new Match(requester.Id, receiver.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => setup.SaveChangesAsync());
        setup.ChangeTracker.Clear();

        await using var mediaFirst = new MindMatchDbContext(options);
        await using var mediaSecond = new MindMatchDbContext(options);
        var firstMediaCopy = await mediaFirst.MediaAssets.SingleAsync(x => x.Id == messageMedia.Id);
        var secondMediaCopy = await mediaSecond.MediaAssets.SingleAsync(x => x.Id == messageMedia.Id);
        firstMediaCopy.BindToConversation(mediaConversation.Id);
        await mediaFirst.SaveChangesAsync();
        secondMediaCopy.BindToConversation(otherConversation.Id);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => mediaSecond.SaveChangesAsync());

        var call = new Call(mediaConversation.Id, requester.Id, receiver.Id, CallType.Audio);
        setup.Calls.Add(call);
        await setup.SaveChangesAsync();
        await using var callFirst = new MindMatchDbContext(options);
        await using var callSecond = new MindMatchDbContext(options);
        var firstCallCopy = await callFirst.Calls.SingleAsync(x => x.Id == call.Id);
        var secondCallCopy = await callSecond.Calls.SingleAsync(x => x.Id == call.Id);
        firstCallCopy.Answer();
        await callFirst.SaveChangesAsync();
        secondCallCopy.Reject();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => callSecond.SaveChangesAsync());

        var refreshToken = new RefreshToken(requester.Id, "integration-refresh-hash", DateTime.UtcNow.AddDays(1));
        setup.RefreshTokens.Add(refreshToken);
        await setup.SaveChangesAsync();
        await using var tokenFirst = new MindMatchDbContext(options);
        await using var tokenSecond = new MindMatchDbContext(options);
        var firstTokenCopy = await tokenFirst.RefreshTokens.SingleAsync(x => x.Id == refreshToken.Id);
        var secondTokenCopy = await tokenSecond.RefreshTokens.SingleAsync(x => x.Id == refreshToken.Id);
        firstTokenCopy.Revoke();
        await tokenFirst.SaveChangesAsync();
        secondTokenCopy.Revoke();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => tokenSecond.SaveChangesAsync());

        var resetToken = new PasswordResetToken(requester.Id, $"reset-{Guid.NewGuid():N}", DateTime.UtcNow.AddMinutes(30));
        setup.PasswordResetTokens.Add(resetToken);
        await setup.SaveChangesAsync();
        await using var resetFirst = new MindMatchDbContext(options);
        await using var resetSecond = new MindMatchDbContext(options);
        var firstResetCopy = await resetFirst.PasswordResetTokens.SingleAsync(x => x.Id == resetToken.Id);
        var secondResetCopy = await resetSecond.PasswordResetTokens.SingleAsync(x => x.Id == resetToken.Id);
        firstResetCopy.MarkUsed(DateTime.UtcNow);
        await resetFirst.SaveChangesAsync();
        secondResetCopy.MarkUsed(DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => resetSecond.SaveChangesAsync());

        await using var cleanup = new MindMatchDbContext(options);
        var resetTokenRepository = new MindMatch.Infrastructure.Repositories.PasswordResetTokenRepository(cleanup);
        var refreshTokenRepository = new MindMatch.Infrastructure.Repositories.RefreshTokenRepository(cleanup);
        Assert.Equal(1, await resetTokenRepository.DeleteInactiveBatchAsync(DateTime.UtcNow, 10));
        Assert.Equal(1, await refreshTokenRepository.DeleteInactiveBatchAsync(DateTime.UtcNow, 10));
        await cleanup.SaveChangesAsync();
        Assert.False(await cleanup.PasswordResetTokens.AnyAsync(x => x.Id == resetToken.Id));
        Assert.False(await cleanup.RefreshTokens.AnyAsync(x => x.Id == refreshToken.Id));
    }

    private static void EnsureIsDisposableDatabase(string connectionString)
    {
        var database = new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Database ?? string.Empty;
        if (!database.StartsWith("mindmatch_integration_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("PostgreSQL integration tests only run on a database named mindmatch_integration_test_*.");
    }
}
