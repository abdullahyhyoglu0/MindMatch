
using MindMatch.Domain.Entities;
using Xunit;

namespace MindMatch.Test.Domain;

public class ConversationTests
{
    [Fact]
    public void Constructor_ShouldCreateActiveConversation()
    {
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var conversation = new Conversation(user1Id, user2Id);

        Assert.True(conversation.IsActive);
        Assert.Null(conversation.LastMessageAtUtc);
        Assert.Empty(conversation.Messages);
        Assert.Empty(conversation.Calls);
        Assert.Empty(conversation.Matches);
    }

    [Fact]
    public void Constructor_ShouldStoreUserIdsInConsistentOrder()
    {
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var first = new Conversation(user1Id, user2Id);
        var second = new Conversation(user2Id, user1Id);

        Assert.Equal(first.User1Id, second.User1Id);
        Assert.Equal(first.User2Id, second.User2Id);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenFirstUserIdIsEmpty()
    {
        var action = () => new Conversation(Guid.Empty, Guid.NewGuid());

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSecondUserIdIsEmpty()
    {
        var action = () => new Conversation(Guid.NewGuid(), Guid.Empty);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenUsersAreSame()
    {
        var userId = Guid.NewGuid();

        var action = () => new Conversation(userId, userId);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        var conversation = new Conversation(Guid.NewGuid(), Guid.NewGuid());

        conversation.Deactivate();

        Assert.False(conversation.IsActive);
    }

    [Fact]
    public void Activate_ShouldSetIsActiveToTrue()
    {
        var conversation = new Conversation(Guid.NewGuid(), Guid.NewGuid());
        conversation.Deactivate();

        conversation.Activate();

        Assert.True(conversation.IsActive);
    }

    [Fact]
    public void UpdateLastMessageTime_ShouldUpdateTimestamp()
    {
        var conversation = new Conversation(Guid.NewGuid(), Guid.NewGuid());
        var messageTime = DateTime.UtcNow;

        conversation.UpdateLastMessageTime(messageTime);

        Assert.Equal(messageTime, conversation.LastMessageAtUtc);
    }
}
