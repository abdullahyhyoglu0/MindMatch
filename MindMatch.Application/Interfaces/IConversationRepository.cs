
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Interfaces;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<Conversation?> GetByUsersAsync(
        Guid user1Id,
        Guid user2Id,
        CancellationToken cancellationToken = default);

    Task<List<Conversation>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default);
}
