using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class MessageRequestRepository : IMessageRequestRepository
{
    private readonly MindMatchDbContext _context;
    public MessageRequestRepository(MindMatchDbContext context) => _context = context;

    public Task<MessageRequest?> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _context.MessageRequests.SingleOrDefaultAsync(x => x.ConversationId == conversationId, cancellationToken);

    public Task<List<MessageRequest>> GetIncomingAsync(Guid receiverId, CancellationToken cancellationToken = default) =>
        _context.MessageRequests.Where(x => x.ReceiverId == receiverId && x.Status == MessageRequestStatus.Pending &&
                _context.Users.Any(sender => sender.Id == x.SenderId && sender.IsActive) &&
                !_context.UserBlocks.Any(block =>
                    (block.BlockerId == x.SenderId && block.BlockedUserId == x.ReceiverId) ||
                    (block.BlockerId == x.ReceiverId && block.BlockedUserId == x.SenderId)))
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task AddAsync(MessageRequest request, CancellationToken cancellationToken = default) =>
        _context.MessageRequests.AddAsync(request, cancellationToken).AsTask();
}
