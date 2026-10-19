using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MindMatch.Api.Services;

public sealed class RedisRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RedisRateLimiter _limiter;

    public RedisRateLimitMiddleware(RequestDelegate next, RedisRateLimiter limiter)
    { _next = next; _limiter = limiter; }

    public async Task InvokeAsync(HttpContext context)
    {
        var policyName = context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        if (policyName is null)
        {
            await _next(context);
            return;
        }

        var claimedIdentity = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        var identity = Guid.TryParse(claimedIdentity, out var userId)
            ? userId.ToString("N")
            : claimedIdentity ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _limiter.TryConsumeAsync(policyName, identity);
        context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        if (result.Allowed)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = result.RetryAfterSeconds.ToString();
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Çok fazla istek gönderildi."
        });
    }
}
