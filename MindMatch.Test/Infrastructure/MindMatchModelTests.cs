using Microsoft.EntityFrameworkCore;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Test.Infrastructure;

public sealed class MindMatchModelTests
{
    [Fact]
    public void EveryMappedEntity_HasTableAndPrimaryKey()
    {
        var options = new DbContextOptionsBuilder<MindMatchDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=test;Password=test")
            .Options;
        using var context = new MindMatchDbContext(options);

        var entityTypes = context.Model.GetEntityTypes().ToArray();
        Assert.True(entityTypes.Length >= 20);
        Assert.All(entityTypes, entity =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entity.GetTableName()), entity.Name);
            Assert.NotNull(entity.FindPrimaryKey());
        });
    }

    [Fact]
    public void CoreSocialModel_HasExpectedUniquenessIndexesAndDeleteRules()
    {
        var options = new DbContextOptionsBuilder<MindMatchDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=test;Password=test")
            .Options;
        using var context = new MindMatchDbContext(options);
        var model = context.Model;
        var conversations = model.FindEntityType(typeof(Conversation))!;
        var messages = model.FindEntityType(typeof(Message))!;
        var deletions = model.FindEntityType(typeof(MessageDeletion))!;
        var blocks = model.FindEntityType(typeof(UserBlock))!;
        var matches = model.FindEntityType(typeof(Match))!;
        var calls = model.FindEntityType(typeof(Call))!;
        var reports = model.FindEntityType(typeof(UserReport))!;
        var evidence = model.FindEntityType(typeof(ReportedMessage))!;

        AssertIndex(conversations, true, nameof(Conversation.User1Id), nameof(Conversation.User2Id));
        AssertIndex(messages, false, nameof(Message.ExpiresAtUtc));
        AssertIndex(messages, false, nameof(Message.ConversationId), nameof(Message.CreatedAtUtc));
        AssertIndex(deletions, true, nameof(MessageDeletion.MessageId), nameof(MessageDeletion.UserId));
        AssertIndex(blocks, true, nameof(UserBlock.BlockerId), nameof(UserBlock.BlockedUserId));
        AssertIndex(matches, true, nameof(Match.UserPairKey));
        Assert.True(matches.FindProperty(nameof(Match.Version))!.IsConcurrencyToken);
        var messageRequest = model.FindEntityType(typeof(MessageRequest))!;
        Assert.True(messageRequest.FindProperty(nameof(MessageRequest.Version))!.IsConcurrencyToken);
        Assert.True(model.FindEntityType(typeof(RefreshToken))!
            .FindProperty(nameof(RefreshToken.Version))!.IsConcurrencyToken);
        Assert.True(model.FindEntityType(typeof(PasswordResetToken))!
            .FindProperty(nameof(PasswordResetToken.Version))!.IsConcurrencyToken);
        AssertIndex(reports, false, nameof(UserReport.Status), nameof(UserReport.CreatedAtUtc));
        AssertIndex(evidence, false, nameof(ReportedMessage.EvidenceExpiresAtUtc));
        AssertIndex(model.FindEntityType(typeof(PremiumEntitlement))!, true,
            nameof(PremiumEntitlement.Source), nameof(PremiumEntitlement.ExternalReference));
        Assert.True(calls.FindProperty(nameof(Call.Version))!.IsConcurrencyToken);

        Assert.Equal(DeleteBehavior.Restrict, conversations.GetForeignKeys()
            .Single(x => x.Properties.Any(p => p.Name == nameof(Conversation.User1Id))).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, messages.GetForeignKeys()
            .Single(x => x.Properties.Any(p => p.Name == nameof(Message.ConversationId))).DeleteBehavior);
        Assert.Equal(DeleteBehavior.SetNull, evidence.GetForeignKeys()
            .Single(x => x.Properties.Any(p => p.Name == nameof(ReportedMessage.MessageId))).DeleteBehavior);
    }

    [Fact]
    public void NewDiscoveryAndMusicEntities_HaveExpectedTablesAndUniqueness()
    {
        var options = new DbContextOptionsBuilder<MindMatchDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=test;Password=test")
            .Options;
        using var context = new MindMatchDbContext(options);
        var decisions = context.Model.FindEntityType(typeof(DiscoveryDecision));
        var music = context.Model.FindEntityType(typeof(UserMusicPreference));
        var profile = context.Model.FindEntityType(typeof(UserProfile));
        var outbox = context.Model.FindEntityType(typeof(RealtimeOutboxMessage));
        var storyView = context.Model.FindEntityType(typeof(StoryView));
        var reportedMessage = context.Model.FindEntityType(typeof(ReportedMessage));
        var mediaAsset = context.Model.FindEntityType(typeof(MediaAsset));
        var user = context.Model.FindEntityType(typeof(User));
        var profilePhoto = context.Model.FindEntityType(typeof(ProfilePhoto));

        Assert.Equal("discovery_decisions", decisions?.GetTableName());
        Assert.Equal("user_music_preferences", music?.GetTableName());
        Assert.Equal("realtime_outbox_messages", outbox?.GetTableName());
        var sequence = outbox?.FindProperty(nameof(RealtimeOutboxMessage.Sequence));
        Assert.NotNull(sequence);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd, sequence!.ValueGenerated);
        Assert.Contains(outbox!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(RealtimeOutboxMessage.DestinationType),
                nameof(RealtimeOutboxMessage.DestinationId),
                nameof(RealtimeOutboxMessage.Sequence)]));
        Assert.Contains(storyView!.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(StoryView.StoryId), nameof(StoryView.ViewerId)]));
        Assert.Contains(profile!.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(UserProfile.NormalizedNickname)]));
        Assert.Contains(decisions!.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(DiscoveryDecision.ViewerId), nameof(DiscoveryDecision.TargetUserId)]));
        Assert.Contains(music!.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(UserMusicPreference.UserId), nameof(UserMusicPreference.NormalizedGenre)]));
        Assert.Contains(reportedMessage!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(ReportedMessage.EvidenceExpiresAtUtc)]));
        Assert.Contains(mediaAsset!.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MediaAsset.CreatedAtUtc)]));
        Assert.True(mediaAsset.FindProperty(nameof(MediaAsset.BindingVersion))!.IsConcurrencyToken);
        Assert.Equal(DeleteBehavior.Restrict, mediaAsset.GetForeignKeys().Single(x =>
            x.Properties.Any(p => p.Name == nameof(MediaAsset.ConversationId))).DeleteBehavior);
        Assert.Contains(user!.GetIndexes(), index => index.IsUnique == false &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(User.IsFrozenByAdmin), nameof(User.AdminFrozenUntilUtc)]));
        Assert.Contains(profilePhoto!.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(ProfilePhoto.UserId)]) &&
            index.GetFilter() == "\"IsPrimary\" = TRUE");
    }

    private static void AssertIndex(Microsoft.EntityFrameworkCore.Metadata.IEntityType entity,
        bool unique, params string[] propertyNames) =>
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique == unique &&
            index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
}
