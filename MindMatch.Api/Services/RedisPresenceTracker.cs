using System.Collections.Concurrent;
using StackExchange.Redis;

namespace MindMatch.Api.Services;

public sealed class RedisPresenceTracker
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(90);
    private readonly IDatabase _database;
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _localConnections = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _userLocks = new();

    public RedisPresenceTracker(IConnectionMultiplexer redis) => _database = redis.GetDatabase();

    public async Task ConnectedAsync(Guid userId, string connectionId)
    {
        var userLock = _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync();
        try
        {
            var connections = _localConnections.GetOrAdd(userId, _ => new());
            connections[connectionId] = 0;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var key = OnlineKey(userId);
            await _database.SortedSetAddAsync(key, connectionId, now);
            await _database.KeyExpireAsync(key, Lease + TimeSpan.FromSeconds(10));
        }
        finally { userLock.Release(); }
    }

    public async Task DisconnectedAsync(Guid userId, string connectionId)
    {
        var userLock = _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync();
        try
        {
            if (_localConnections.TryGetValue(userId, out var connections))
            {
                connections.TryRemove(connectionId, out _);
                if (connections.IsEmpty) _localConnections.TryRemove(userId, out _);
            }

            const string script = """
            redis.call('ZREM', KEYS[1], ARGV[1])
            local count = redis.call('ZCARD', KEYS[1])
            if count == 0 then
              redis.call('DEL', KEYS[1])
              redis.call('SET', KEYS[2], ARGV[2])
            end
            return count
            """;
            await _database.ScriptEvaluateAsync(script,
                [OnlineKey(userId), LastSeenKey(userId)],
                [connectionId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()]);
        }
        finally { userLock.Release(); }
    }

    public async Task<(bool IsOnline, DateTime? LastSeenAtUtc)> GetStatusAsync(Guid userId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const string script = """
            local latest = redis.call('ZREVRANGE', KEYS[1], 0, 0, 'WITHSCORES')
            local removed = redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
            local count = redis.call('ZCARD', KEYS[1])
            if count == 0 and removed > 0 then
              redis.call('DEL', KEYS[1])
              if #latest > 1 then
                redis.call('SET', KEYS[2], latest[2], 'EX', 2592000)
              elseif redis.call('EXISTS', KEYS[2]) == 0 then
                redis.call('SET', KEYS[2], ARGV[2], 'EX', 2592000)
              end
            end
            local seen = redis.call('GET', KEYS[2])
            return {count, seen or ''}
            """;
        var result = (RedisResult[]?)await _database.ScriptEvaluateAsync(script,
            [OnlineKey(userId), LastSeenKey(userId)],
            [now - (long)Lease.TotalMilliseconds, DateTimeOffset.FromUnixTimeMilliseconds(now - (long)Lease.TotalMilliseconds).ToString("O")]);
        if (result is null || result.Length < 2)
            return (false, null);
        var count = (long)result[0];
        var text = (string?)result[1];
        DateTime? seenAtUtc = long.TryParse(text, out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime
            : DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc) : null;
        return (count > 0, seenAtUtc);
    }

    public async Task RefreshLocalLeasesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var (userId, connections) in _localConnections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userLock = _userLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
            await userLock.WaitAsync(cancellationToken);
            try
            {
                if (!_localConnections.TryGetValue(userId, out var current) || current.IsEmpty) continue;
                var key = OnlineKey(userId);
                foreach (var connectionId in current.Keys)
                    await _database.SortedSetAddAsync(key, connectionId, now);
                await _database.KeyExpireAsync(key, Lease + TimeSpan.FromSeconds(10));
            }
            finally { userLock.Release(); }
        }
    }

    private static RedisKey OnlineKey(Guid userId) => $"presence:online:{userId:N}";
    private static RedisKey LastSeenKey(Guid userId) => $"presence:last-seen:{userId:N}";
}

public sealed class RedisPresenceHeartbeat : BackgroundService
{
    private readonly RedisPresenceTracker _tracker;
    private readonly ILogger<RedisPresenceHeartbeat> _logger;
    public RedisPresenceHeartbeat(RedisPresenceTracker tracker, ILogger<RedisPresenceHeartbeat> logger)
    { _tracker = tracker; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await _tracker.RefreshLocalLeasesAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Redis presence lease yenilenemedi."); }
        }
    }
}
