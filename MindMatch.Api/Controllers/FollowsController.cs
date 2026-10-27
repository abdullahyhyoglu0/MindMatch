using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/follows")]
public sealed class FollowsController : ControllerBase
{
    private readonly IUserFollowService _follows;
    public FollowsController(IUserFollowService follows) => _follows = follows;

    [HttpPost("{userId:guid}")]
    [EnableRateLimiting("follow-write")]
    public async Task<IActionResult> Follow(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var viewerId)) return Unauthorized();
        try { return Ok(await _follows.FollowAsync(viewerId, userId, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        { return Conflict(new { message = "Takip isteği aynı anda değiştirilmiş. İlişkiyi yenileyip tekrar deneyin." }); }
    }

    [HttpDelete("{userId:guid}")]
    [EnableRateLimiting("follow-write")]
    public async Task<IActionResult> Unfollow(Guid userId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var viewerId)) return Unauthorized();
        try { await _follows.UnfollowAsync(viewerId, userId, cancellationToken); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("followers/{followerId:guid}")]
    [EnableRateLimiting("follow-write")]
    public async Task<IActionResult> RemoveFollower(Guid followerId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _follows.RemoveFollowerAsync(userId, followerId, cancellationToken); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("requests/incoming")]
    public async Task<IActionResult> Incoming(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await _follows.GetIncomingAsync(userId, cancellationToken));
    }

    [HttpPost("requests/{followerId:guid}/accept")]
    [EnableRateLimiting("follow-write")]
    public async Task<IActionResult> Accept(Guid followerId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _follows.AcceptAsync(userId, followerId, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("requests/{followerId:guid}/reject")]
    [EnableRateLimiting("follow-write")]
    public async Task<IActionResult> Reject(Guid followerId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { await _follows.RejectAsync(userId, followerId, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{userId:guid}/followers")]
    public async Task<IActionResult> Followers(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) => await GetListAsync(userId, page, pageSize, false, cancellationToken);

    [HttpGet("{userId:guid}/following")]
    public async Task<IActionResult> Following(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) => await GetListAsync(userId, page, pageSize, true, cancellationToken);

    private async Task<IActionResult> GetListAsync(Guid targetId, int page, int pageSize, bool following,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var viewerId)) return Unauthorized();
        try
        {
            var result = following
                ? await _follows.GetFollowingAsync(viewerId, targetId, page, pageSize, cancellationToken)
                : await _follows.GetFollowersAsync(viewerId, targetId, page, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
