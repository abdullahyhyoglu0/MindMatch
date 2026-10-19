using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly MindMatchDbContext _context;

    public MessageRepository(MindMatchDbContext context)
    {
        _context = context;
    }

    public async Task<Message?> GetByIdAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .SingleOrDefaultAsync(
                x => x.Id == messageId,
                cancellationToken);
    }

    public Task<Message?> GetFirstBySenderAsync(
        Guid conversationId,
        Guid senderId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        return _context.Messages.Where(x => x.ConversationId == conversationId && x.SenderId == senderId &&
                !x.IsDeletedForEveryone && (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc.Value > nowUtc))
            .OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Message>> GetVisiblePageAsync(
        Guid conversationId,
        Guid userId,
        DateTime? beforeCreatedAtUtc,
        Guid? beforeMessageId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var query = _context.Messages.Where(x =>
            x.ConversationId == conversationId &&
            !x.IsDeletedForEveryone &&
            (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc.Value > nowUtc) &&
            !_context.MessageDeletions.Any(d =>
                d.MessageId == x.Id && d.UserId == userId));

        if (beforeCreatedAtUtc.HasValue && beforeMessageId.HasValue)
        {
            var before = DateTime.SpecifyKind(
                beforeCreatedAtUtc.Value, DateTimeKind.Utc);
            var beforeId = beforeMessageId.Value;
            query = query.Where(x => x.CreatedAtUtc < before ||
                (x.CreatedAtUtc == before && x.Id.CompareTo(beforeId) < 0));
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddDeletionAsync(
        MessageDeletion deletion,
        CancellationToken cancellationToken = default)
    {
        await _context.MessageDeletions.AddAsync(deletion, cancellationToken);
    }

    public Task<bool> HasDeletionAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _context.MessageDeletions.AnyAsync(x =>
            x.MessageId == messageId && x.UserId == userId,
            cancellationToken);

    public Task<List<Message>> GetByIdsAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default) =>
        _context.Messages.Where(x => messageIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

    public Task<Message?> GetByMediaAssetIdAsync(Guid mediaAssetId, CancellationToken cancellationToken = default) =>
        _context.Messages.FirstOrDefaultAsync(x => x.MediaAssetId == mediaAssetId && !x.IsDeletedForEveryone, cancellationToken);

    public Task<List<Message>> GetExpiredBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) =>
        _context.Messages.Where(x => x.ExpiresAtUtc <= nowUtc && !x.IsDeletedForEveryone)
            .OrderBy(x => x.ExpiresAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task AddAsync(
        Message message,
        CancellationToken cancellationToken = default)
    {
        await _context.Messages.AddAsync(message, cancellationToken);
    }
}
