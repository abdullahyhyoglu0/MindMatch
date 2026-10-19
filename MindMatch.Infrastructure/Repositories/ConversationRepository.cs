
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly MindMatchDbContext _context;

    public ConversationRepository(MindMatchDbContext context)
    {
        _context = context;
    }

    public async Task<Conversation?> GetByIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .SingleOrDefaultAsync(
                x => x.Id == conversationId,
                cancellationToken);
    }

    public async Task<Conversation?> GetByUsersAsync(
        Guid user1Id,
        Guid user2Id,
        CancellationToken cancellationToken = default)
    {
        var firstUserId = user1Id.CompareTo(user2Id) < 0
            ? user1Id
            : user2Id;

        var secondUserId = user1Id.CompareTo(user2Id) < 0
            ? user2Id
            : user1Id;

        return await _context.Conversations
            .SingleOrDefaultAsync(
                x => x.User1Id == firstUserId
                     && x.User2Id == secondUserId,
                cancellationToken);
    }

    public async Task<List<Conversation>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .Where(x =>
                !_context.MessageRequests.Any(request => request.ConversationId == x.Id &&
                    request.Status != MessageRequestStatus.Accepted) &&
                ((x.User1Id == userId &&
                  !x.User1HasHiddenConversation) ||
                (x.User2Id == userId &&
                 !x.User2HasHiddenConversation)))
            .OrderByDescending(x => x.LastMessageAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default)
    {
        await _context.Conversations.AddAsync(
            conversation,
            cancellationToken);
    }
}
