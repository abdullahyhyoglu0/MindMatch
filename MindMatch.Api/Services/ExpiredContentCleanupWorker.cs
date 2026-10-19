using MindMatch.Application.Interfaces;
using StackExchange.Redis;

namespace MindMatch.Api.Services;

public sealed class ExpiredContentCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredContentCleanupWorker> _logger;
    private readonly IDatabase _redis;
    private static readonly RedisKey LockKey = "mindmatch:lock:expired-content-cleanup";
    private static readonly TimeSpan LockLease = TimeSpan.FromMinutes(2);

    public ExpiredContentCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<ExpiredContentCleanupWorker> logger,
        IConnectionMultiplexer redis)
    { _scopeFactory = scopeFactory; _logger = logger; _redis = redis.GetDatabase(); }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            var lockToken = Guid.NewGuid().ToString("N");
            var acquired = false;
            try
            {
                acquired = await _redis.StringSetAsync(LockKey, lockToken, LockLease, When.NotExists);
                if (acquired)
                    await RunLockedBatchAsync(lockToken, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Background cleanup batch failed; the next batch will retry.");
            }
            finally
            {
                if (acquired)
                {
                    try
                    {
                        await _redis.ScriptEvaluateAsync(
                            "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end",
                            [LockKey], [lockToken]);
                    }
                    catch (Exception exception)
                    { _logger.LogWarning(exception, "Background cleanup lock release failed; its lease will expire."); }
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunLockedBatchAsync(string lockToken, CancellationToken stoppingToken)
    {
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewal = RenewLockAsync(lockToken, runCancellation, renewCancellation.Token);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var cleanup = scope.ServiceProvider.GetRequiredService<IBackgroundCleanupService>();
            await cleanup.ExecuteBatchAsync(runCancellation.Token);
        }
        finally
        {
            renewCancellation.Cancel();
            try { await renewal; }
            catch (OperationCanceledException) when (renewCancellation.IsCancellationRequested) { }
        }
    }

    private async Task RenewLockAsync(string lockToken, CancellationTokenSource runCancellation, CancellationToken cancellationToken)
    {
        const string renewScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) else return 0 end";
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var renewed = (long)await _redis.ScriptEvaluateAsync(renewScript,
                    [LockKey], [lockToken, (long)LockLease.TotalMilliseconds]);
                if (renewed == 0)
                {
                    _logger.LogWarning("Background cleanup lost its distributed lock; stopping this batch.");
                    runCancellation.Cancel();
                    return;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Background cleanup lock renewal failed; stopping this batch.");
                runCancellation.Cancel();
                return;
            }
        }
    }
}
