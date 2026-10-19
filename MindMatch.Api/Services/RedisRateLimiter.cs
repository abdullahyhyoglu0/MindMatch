using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace MindMatch.Api.Services;

public sealed record RedisRateLimitResult(bool Allowed, int Limit, long Remaining, int RetryAfterSeconds);

public sealed class RedisRateLimiter
{
    private sealed record Rule(int Limit, TimeSpan Window);
    private static readonly IReadOnlyDictionary<string, Rule> Rules = new Dictionary<string, Rule>
    {
        ["auth-write"] = new(10, TimeSpan.FromMinutes(5)),
        ["message-write"] = new(30, TimeSpan.FromMinutes(1)),
        ["match-write"] = new(20, TimeSpan.FromHours(1)),
        ["report-write"] = new(5, TimeSpan.FromHours(1)),
        ["media-upload"] = new(20, TimeSpan.FromHours(1)),
        ["hub-events"] = new(120, TimeSpan.FromMinutes(1)),
        ["call-signaling"] = new(300, TimeSpan.FromMinutes(1))
    };
    private const string IncrementScript = "local count = redis.call('INCR', KEYS[1]); if count == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]); end; return {count, redis.call('PTTL', KEYS[1])}";
    private readonly IDatabase _redis;
    public RedisRateLimiter(IConnectionMultiplexer redis) => _redis = redis.GetDatabase();

    public async Task<RedisRateLimitResult> TryConsumeAsync(string policyName, string identity)
    {
        if (!Rules.TryGetValue(policyName, out var rule))
            throw new InvalidOperationException($"Bilinmeyen rate limit politikası: {policyName}");
        var identityHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var windowMilliseconds = (long)rule.Window.TotalMilliseconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var bucket = now / windowMilliseconds;
        var remainingWindow = windowMilliseconds - (now % windowMilliseconds);
        var key = $"ratelimit:{policyName}:{identityHash}:{bucket}";
        var result = (RedisResult[]?)await _redis.ScriptEvaluateAsync(IncrementScript, [key], [remainingWindow]);
        if (result is null || result.Length < 2)
            throw new InvalidOperationException("Redis rate limit yanıtı geçersiz.");
        var count = (long)result[0];
        var retryMilliseconds = Math.Max(1, (long)result[1]);
        return new RedisRateLimitResult(count <= rule.Limit, rule.Limit,
            Math.Max(0, rule.Limit - count), Math.Max(1, (int)Math.Ceiling(retryMilliseconds / 1000d)));
    }
}
