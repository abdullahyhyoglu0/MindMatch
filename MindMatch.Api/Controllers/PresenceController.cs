using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindMatch.Api.Services;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/presence")]
public sealed class PresenceController : ControllerBase
{
    private readonly RedisPresenceTracker _presence;
    private readonly IUserRepository _users;
    private readonly IUserProfileRepository _profiles;
    private readonly IUserBlockRepository _blocks;

    public PresenceController(RedisPresenceTracker presence, IUserRepository users,
        IUserProfileRepository profiles, IUserBlockRepository blocks)
    { _presence = presence; _users = users; _profiles = profiles; _blocks = blocks; }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken)
    {
        var viewerId = GetUserId();
        if (viewerId == Guid.Empty) return Unauthorized();
        if (userId != viewerId && await _blocks.IsBlockedEitherWayAsync(viewerId, userId, cancellationToken))
            return NotFound();
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        var profile = await _profiles.GetByUserIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || profile is null ||
            (userId != viewerId && !profile.IsProfileVisible)) return NotFound();

        var (online, lastSeen) = await _presence.GetStatusAsync(userId);
        if (userId != viewerId)
        {
            if (!profile.ShowOnlineStatus) online = false;
            else if (online) lastSeen = null;
            if (!profile.ShowLastSeen) lastSeen = null;
        }
        return Ok(new PresenceResponse(online, lastSeen));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public sealed record PresenceResponse(bool IsOnline, DateTime? LastSeenAtUtc);
}
