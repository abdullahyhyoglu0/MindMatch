using MindMatch.Domain.Entities;

namespace MindMatch.Test.Domain;

public sealed class MediaAssetTests
{
    [Fact]
    public void ConversationBinding_IsIdempotentAndCannotBeChanged()
    {
        var asset = new MediaAsset(Guid.NewGuid(), MediaCategory.MessageImage,
            "message/test.jpg", "image/jpeg", 10);
        var conversationId = Guid.NewGuid();

        asset.BindToConversation(conversationId);
        var version = asset.BindingVersion;
        asset.BindToConversation(conversationId);

        Assert.Equal(conversationId, asset.ConversationId);
        Assert.Equal(version, asset.BindingVersion);
        Assert.Throws<InvalidOperationException>(() => asset.BindToConversation(Guid.NewGuid()));
    }

    [Fact]
    public void MarkDeleted_ChangesConcurrencyVersion()
    {
        var asset = new MediaAsset(Guid.NewGuid(), MediaCategory.MessageImage,
            "message/test.jpg", "image/jpeg", 10);
        var oldVersion = asset.BindingVersion;

        asset.MarkDeleted();

        Assert.True(asset.IsDeleted);
        Assert.NotEqual(oldVersion, asset.BindingVersion);
    }
}
