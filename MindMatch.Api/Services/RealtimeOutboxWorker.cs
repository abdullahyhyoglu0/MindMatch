using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using MindMatch.Infrastructure.Persistence;
using StackExchange.Redis;

namespace MindMatch.Api.Services;

public sealed class RealtimeOutboxWorker : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxEventsPerRun = 500;
    private static readonly RedisKey LockKey = "mindmatch:lock:realtime-outbox";
    private static readonly TimeSpan LockLease = TimeSpan.FromMinutes(2);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDatabase _redis;
    private readonly IMessageEventPublisher _publisher;
    private readonly ILogger<RealtimeOutboxWorker> _logger;

    public RealtimeOutboxWorker(IServiceScopeFactory scopeFactory, IConnectionMultiplexer redis,
        IMessageEventPublisher publisher, ILogger<RealtimeOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _redis = redis.GetDatabase();
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try { await ProcessBatchWithLockAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { _logger.LogError(exception, "Realtime outbox batch failed; it will retry."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessBatchWithLockAsync(CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        if (!await _redis.StringSetAsync(LockKey, token, LockLease, When.NotExists)) return;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRealtimeOutboxRepository>();
            var messages = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var context = scope.ServiceProvider.GetRequiredService<MindMatchDbContext>();
            var processedCount = 0;
            while (processedCount < MaxEventsPerRun)
            {
                var records = await repository.GetPendingBatchAsync(DateTime.UtcNow, BatchSize, cancellationToken);
                if (records.Count == 0) break;
                foreach (var record in records)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var renewed = (long)await _redis.ScriptEvaluateAsync(
                        "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) else return 0 end",
                        [LockKey], [token, (long)LockLease.TotalMilliseconds]);
                    if (renewed == 0) throw new InvalidOperationException("Outbox worker lost its Redis lock.");
                    try
                    {
                        if (record.DestinationType == RealtimeDestinationType.User)
                        {
                            var recipient = await users.GetByIdAsync(record.DestinationId, cancellationToken);
                            if (recipient is null || !recipient.IsActive)
                            {
                                record.MarkProcessed(DateTime.UtcNow);
                                await context.SaveChangesAsync(cancellationToken);
                                processedCount++;
                                continue;
                            }
                        }

                        var payload = JsonNode.Parse(record.PayloadJson) as JsonObject
                            ?? throw new InvalidOperationException("Realtime event payload must be a JSON object.");
                        payload["eventId"] = record.Id.ToString("N");
                        payload["eventCreatedAtUtc"] = record.CreatedAtUtc;
                        if (record.EventName == "MessageCreated" ||
                            (record.EventName == "ConversationUpdated" && payload["messageId"] is not null))
                        {
                            var messageId = (record.EventName == "MessageCreated" ? payload["id"] : payload["messageId"])?.GetValue<Guid>()
                                ?? throw new InvalidOperationException("MessageCreated payload has no message ID.");
                            var currentMessage = await messages.GetByIdAsync(messageId, cancellationToken);
                            if (currentMessage is null || currentMessage.IsDeletedForEveryone ||
                                currentMessage.ExpiresAtUtc <= DateTime.UtcNow ||
                                await messages.HasDeletionAsync(messageId, record.DestinationId, cancellationToken))
                            {
                                record.MarkProcessed(DateTime.UtcNow);
                                await context.SaveChangesAsync(cancellationToken);
                                processedCount++;
                                continue;
                            }
                        }
                        if (record.DestinationType == RealtimeDestinationType.User)
                            await _publisher.PublishToUserAsync(record.DestinationId, record.EventName, payload, cancellationToken);
                        else
                            await _publisher.PublishToConversationAsync(record.DestinationId, record.EventName, payload, cancellationToken);
                        record.MarkProcessed(DateTime.UtcNow);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        record.RecordFailure(DateTime.UtcNow, exception.GetType().Name);
                        _logger.LogWarning("Realtime event {OutboxId} could not be published ({ErrorType}).",
                            record.Id, exception.GetType().Name);
                    }
                    await context.SaveChangesAsync(cancellationToken);
                    processedCount++;
                    if (processedCount >= MaxEventsPerRun) break;
                }
            }

            await repository.DeleteExpiredBatchAsync(DateTime.UtcNow, BatchSize, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await _redis.ScriptEvaluateAsync(
                "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end",
                [LockKey], [token]);
        }
    }
}
