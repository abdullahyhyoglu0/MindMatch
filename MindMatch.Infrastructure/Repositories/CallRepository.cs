using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;

namespace MindMatch.Infrastructure.Repositories;

public sealed class CallRepository : ICallRepository
{
    private readonly MindMatchDbContext _context;
    public CallRepository(MindMatchDbContext context) => _context = context;
    public Task<Call?> GetByIdAsync(Guid callId, CancellationToken cancellationToken = default) =>
        _context.Calls.SingleOrDefaultAsync(x => x.Id == callId, cancellationToken);
    public Task AddAsync(Call call, CancellationToken cancellationToken = default) =>
        _context.Calls.AddAsync(call, cancellationToken).AsTask();
    public Task<List<Call>> GetMissedBatchAsync(DateTime cutoffUtc, int take, CancellationToken cancellationToken = default) =>
        _context.Calls.Where(x => x.Status == CallStatus.Ringing && x.StartedAtUtc <= cutoffUtc)
            .OrderBy(x => x.StartedAtUtc).Take(take).ToListAsync(cancellationToken);
    public Task<List<Call>> GetByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _context.Calls.Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.StartedAtUtc).Take(100).ToListAsync(cancellationToken);
}
