using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public sealed class BackgroundCleanupService : IBackgroundCleanupService
{
    private const int BatchSize = 100;
    private readonly IMessageRepository _messages;
    private readonly IStoryRepository _stories;
    private readonly ICallRepository _calls;
    private readonly IMediaAssetRepository _assets;
    private readonly IMediaStorage _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeOutboxRepository _outbox;
    private readonly IReportRepository _reports;
    private readonly IUserRepository _users;
    private readonly IPasswordResetTokenRepository _passwordResetTokens;
    private readonly IRefreshTokenRepository _refreshTokens;

    public BackgroundCleanupService(IMessageRepository messages, IStoryRepository stories,
        ICallRepository calls, IMediaAssetRepository assets, IMediaStorage storage,
        IRealtimeOutboxRepository outbox, IReportRepository reports, IUserRepository users,
        IPasswordResetTokenRepository passwordResetTokens, IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork)
    {
        _messages = messages; _stories = stories; _calls = calls; _assets = assets;
        _storage = storage; _outbox = outbox; _reports = reports; _users = users;
        _passwordResetTokens = passwordResetTokens; _refreshTokens = refreshTokens; _unitOfWork = unitOfWork;
    }

    public async Task ExecuteBatchAsync(CancellationToken cancellationToken = default)
    {
        var failures = new List<Exception>();
        var now = DateTime.UtcNow;
        var clearedEvidence = await _reports.ClearExpiredEvidenceBatchAsync(now, BatchSize, cancellationToken);
        if (clearedEvidence > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);

        var deletedResetTokens = await _passwordResetTokens.DeleteInactiveBatchAsync(now, BatchSize, cancellationToken);
        var deletedRefreshTokens = await _refreshTokens.DeleteInactiveBatchAsync(now, BatchSize, cancellationToken);
        if (deletedResetTokens > 0 || deletedRefreshTokens > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        var expiredAdminFreezes = await _users.GetExpiredAdminFrozenBatchAsync(now, BatchSize, cancellationToken);
        foreach (var user in expiredAdminFreezes)
        {
            var adminId = user.FrozenByAdminId;
            if (adminId.HasValue && user.ExpireAdminFreeze(now))
                await _reports.AddAuditAsync(new ModerationAudit(adminId.Value,
                    ModerationActionType.UserFreezeExpired, "Yönetici dondurma süresi sona erdi.", user.Id), cancellationToken);
        }
        if (expiredAdminFreezes.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);

        var expiredMessages = await _messages.GetExpiredBatchAsync(now, BatchSize, cancellationToken);
        foreach (var message in expiredMessages)
        {
            if (message.MediaAssetId.HasValue)
            {
                var asset = await _assets.GetByIdAsync(message.MediaAssetId.Value, cancellationToken);
                asset?.MarkDeleted();
            }
            message.ClearExpiredContent(now);
        }
        if (expiredMessages.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);

        var expiredStories = await _stories.GetExpiredBatchAsync(now, BatchSize, cancellationToken);
        foreach (var story in expiredStories)
        {
            if (story.MediaAssetId.HasValue)
            {
                var asset = await _assets.GetByIdAsync(story.MediaAssetId.Value, cancellationToken);
                asset?.MarkDeleted();
            }
            story.Delete();
        }
        if (expiredStories.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);

        var missedCalls = await _calls.GetMissedBatchAsync(now.AddSeconds(-60), BatchSize, cancellationToken);
        foreach (var call in missedCalls)
        {
            try
            {
                call.MarkAsMissed();
                await _outbox.AddAsync(RealtimeEventOutbox.ToUser(call.CallerId, "CallMissed", new
                {
                    call.Id, call.ConversationId, call.CallerId, call.ReceiverId, call.Status,
                    call.StartedAtUtc, call.EndedAtUtc
                }, call.StartedAtUtc.AddDays(7)), cancellationToken);
            }
            catch (InvalidOperationException exception)
            { failures.Add(exception); }
        }
        if (missedCalls.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);

        var deletedAssets = await _assets.GetDeletedUnreferencedBatchAsync(BatchSize, cancellationToken);
        foreach (var asset in deletedAssets)
        {
            try
            {
                await _storage.DeleteAsync(asset.StorageKey, cancellationToken);
                _assets.Remove(asset);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(new IOException($"Media asset {asset.Id} could not be deleted; cleanup will retry.", exception));
            }
        }
        if (deletedAssets.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);

        var orphanedAssets = await _assets.GetExpiredUnattachedBatchAsync(now.AddHours(-24), BatchSize, cancellationToken);
        foreach (var asset in orphanedAssets)
        {
            asset.MarkDeleted();
            try
            {
                await _storage.DeleteAsync(asset.StorageKey, cancellationToken);
                _assets.Remove(asset);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(new IOException($"Unattached media asset {asset.Id} could not be deleted; cleanup will retry.", exception));
            }
        }
        if (orphanedAssets.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (failures.Count > 0)
            throw new AggregateException("Some cleanup records failed and will be retried.", failures);
    }
}
