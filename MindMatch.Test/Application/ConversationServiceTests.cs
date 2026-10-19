using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class ConversationServiceTests
{
    [Fact]
    public async Task GetConversation_ReturnsItForParticipant()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var conversation = new Conversation(first, second);
        var service = CreateService(conversation, out _, out _);

        var response = await service.GetAsync(conversation.Id, first);

        Assert.Equal(conversation.Id, response.Id);
        Assert.Equal(second, response.OtherUserId);
    }

    [Fact]
    public async Task GetConversation_RejectsNonParticipant()
    {
        var conversation = new Conversation(Guid.NewGuid(), Guid.NewGuid());
        var service = CreateService(conversation, out _, out _);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetAsync(conversation.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task HideConversation_ChangesOnlyRequestingParticipantsView()
    {
        var first = Guid.NewGuid();
        var conversation = new Conversation(first, Guid.NewGuid());
        var service = CreateService(conversation, out var unitOfWork, out _);

        await service.HideConversationAsync(conversation.Id, first);

        Assert.True(conversation.User1Id == first
            ? conversation.User1HasHiddenConversation
            : conversation.User2HasHiddenConversation);
        Assert.False(conversation.User1Id == first
            ? conversation.User2HasHiddenConversation
            : conversation.User1HasHiddenConversation);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private static ConversationService CreateService(Conversation conversation,
        out FakeUnitOfWork unitOfWork, out FakeConversationRepository repository)
    {
        repository = new FakeConversationRepository(conversation);
        unitOfWork = new FakeUnitOfWork();
        return new ConversationService(repository, unitOfWork);
    }

    private sealed class FakeConversationRepository(Conversation conversation) : IConversationRepository
    {
        public Task<Conversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Conversation?>(conversation.Id == conversationId ? conversation : null);
        public Task<Conversation?> GetByUsersAsync(Guid user1Id, Guid user2Id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Conversation?>(null);
        public Task<List<Conversation>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Conversation>());
        public Task AddAsync(Conversation item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
