
using MindMatch.Domain.Entities;
using Xunit;

namespace MindMatch.Test.Domain;

public class MessageTests
{
    [Fact]
    public void Constructor_ShouldCreateTextMessageWithSentStatus()
    {
        var conversationId = Guid.NewGuid();
        var senderId = Guid.NewGuid();

        var message = new Message(
            conversationId,
            senderId,
            MessageType.Text,
            "Merhaba!");

        Assert.Equal(conversationId, message.ConversationId);
        Assert.Equal(senderId, message.SenderId);
        Assert.Equal("Merhaba!", message.Content);
        Assert.Equal(MessageType.Text, message.Type);
        Assert.Equal(MessageStatus.Sent, message.Status);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenConversationIdIsEmpty()
    {
        var action = () => new Message(
            Guid.Empty,
            Guid.NewGuid(),
            MessageType.Text,
            "Merhaba!");

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSenderIdIsEmpty()
    {
        var action = () => new Message(
            Guid.NewGuid(),
            Guid.Empty,
            MessageType.Text,
            "Merhaba!");

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenContentAndMediaUrlAreEmpty()
    {
        var action = () => new Message(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageType.Text);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void MarkAsDelivered_ShouldChangeStatusToDelivered()
    {
        var message = CreateMessage();

        message.MarkAsDelivered();

        Assert.Equal(MessageStatus.Delivered, message.Status);
    }

    [Fact]
    public void MarkAsRead_ShouldChangeStatusToReadAndSetReadTime()
    {
        var message = CreateMessage();

        message.MarkAsRead();

        Assert.Equal(MessageStatus.Read, message.Status);
        Assert.NotNull(message.ReadAtUtc);
    }

    [Fact]
    public void MarkAsDelivered_ShouldNotDowngradeReadStatus()
    {
        var message = CreateMessage();
        message.MarkAsRead();

        message.MarkAsDelivered();

        Assert.Equal(MessageStatus.Read, message.Status);
    }

    [Fact]
    public void DeleteForEveryone_ShouldClearTextAndMedia()
    {
        var senderId = Guid.NewGuid();
        var message = new Message(Guid.NewGuid(), senderId,
            MessageType.Image, "caption", "media-key");

        message.DeleteForEveryone(senderId);

        Assert.True(message.IsDeletedForEveryone);
        Assert.Null(message.Content);
        Assert.Null(message.MediaUrl);
        Assert.NotNull(message.DeletedAtUtc);
    }

    [Fact]
    public void DeleteForEveryone_ShouldRejectNonSender()
    {
        var message = CreateMessage();

        Assert.Throws<UnauthorizedAccessException>(
            () => message.DeleteForEveryone(Guid.NewGuid()));
    }

    [Fact]
    public void ClearExpiredContent_ShouldBeRepeatable()
    {
        var message = CreateMessage();
        var expiry = message.CreatedAtUtc.AddHours(24);
        message.SetExpiration(expiry);

        Assert.True(message.ClearExpiredContent(expiry));
        Assert.False(message.ClearExpiredContent(expiry.AddMinutes(1)));
        Assert.Null(message.Content);
    }

    private static Message CreateMessage()
    {
        return new Message(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageType.Text,
            "Test mesajı");
    }
}
