
using MindMatch.Domain.Entities;
using Xunit;

namespace MindMatch.Test.Domain;

public class MatchTests
{
    [Fact]
    public void Constructor_ShouldCreatePendingMatch()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();

        // Act
        var match = new Match(requesterId, receiverId);

        // Assert
        Assert.Equal(requesterId, match.RequesterId);
        Assert.Equal(receiverId, match.ReceiverId);
        Assert.Equal(MatchStatus.Pending, match.Status);
        Assert.Equal(1, match.Version);
    }

    [Fact]
    public void PairKey_IsStableRegardlessOfRequestDirection()
    {
        var firstUserId = Guid.Parse("f0000000-0000-0000-0000-000000000001");
        var secondUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");

        var firstToSecond = new Match(firstUserId, secondUserId);
        var secondToFirst = new Match(secondUserId, firstUserId);

        Assert.Equal(firstToSecond.UserPairKey, secondToFirst.UserPairKey);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenUsersAreSame()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var action = () => new Match(userId, userId);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Accept_ShouldSetStatusToAccepted_WhenReceiverAccepts()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var match = new Match(requesterId, receiverId);

        // Act
        match.Accept(receiverId);

        // Assert
        Assert.Equal(MatchStatus.Accepted, match.Status);
        Assert.NotNull(match.RespondedAtUtc);
        Assert.Equal(2, match.Version);
    }

    [Fact]
    public void Accept_ShouldThrow_WhenRequesterTriesToAccept()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var match = new Match(requesterId, receiverId);

        // Act
        var action = () => match.Accept(requesterId);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void End_ShouldSetStatusToEnded_WhenParticipantEndsMatch()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var match = new Match(requesterId, receiverId);
        match.Accept(receiverId);

        // Act
        match.End(requesterId);

        // Assert
        Assert.Equal(MatchStatus.Ended, match.Status);
        Assert.NotNull(match.EndedAtUtc);
        Assert.Equal(3, match.Version);
    }

    [Fact]
    public void Accept_ShouldThrow_WhenMatchIsAlreadyEnded()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var match = new Match(requesterId, receiverId);

        match.Accept(receiverId);
        match.End(requesterId);

        // Act
        var action = () => match.Accept(receiverId);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }
}
