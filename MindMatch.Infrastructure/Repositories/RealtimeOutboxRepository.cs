using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class RealtimeOutboxRepository : IRealtimeOutboxRepository
{
    private readonly MindMatchDbContext _context;
    public RealtimeOutboxRepository(MindMatchDbContext context) => _context = context;

    public async Task AddAsync(RealtimeOutboxMessage message, CancellationToken cancellationToken = default) =>
        await _context.RealtimeOutboxMessages.AddAsync(message, cancellationToken);

    public Task<List<RealtimeOutboxMessage>> GetPendingBatchAsync(DateTime nowUtc, int take,
        CancellationToken cancellationToken = default) =>
        _context.RealtimeOutboxMessages.Where(x => x.ProcessedAtUtc == null && x.ExpiresAtUtc > nowUtc && x.NextAttemptAtUtc <= nowUtc &&
                !_context.RealtimeOutboxMessages.Any(previous => previous.ProcessedAtUtc == null &&
                    previous.ExpiresAtUtc > nowUtc && previous.DestinationType == x.DestinationType &&
                    previous.DestinationId == x.DestinationId && previous.Sequence < x.Sequence))
            .OrderBy(x => x.Sequence).Take(take).ToListAsync(cancellationToken);

    public async Task DeleteExpiredBatchAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
    {
        var expired = await _context.RealtimeOutboxMessages.Where(x => x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc).Take(take).ToListAsync(cancellationToken);
        _context.RealtimeOutboxMessages.RemoveRange(expired);
    }
}
