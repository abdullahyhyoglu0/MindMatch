using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/discovery")]
[Authorize]
public sealed class DiscoveryController : ControllerBase
{
    private readonly IDiscoveryService _discovery;
    public DiscoveryController(IDiscoveryService discovery) => _discovery = discovery;

    [HttpGet]
    public async Task<IActionResult> Discover([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? city = null, [FromQuery] bool premiumOnly = false,
        CancellationToken cancellationToken = default)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out var userId)) return Unauthorized();
        try { return Ok(await _discovery.DiscoverAsync(userId, page, pageSize, city, premiumOnly, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("people")]
    public async Task<IActionResult> ListPeople([FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out var userId)) return Unauthorized();
        try { return Ok(await _discovery.ListPeopleAsync(userId, page, pageSize, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{targetUserId:guid}/seen")]
    public Task<IActionResult> Seen(Guid targetUserId, CancellationToken cancellationToken) =>
        RecordDecision(targetUserId, false, cancellationToken);

    [HttpPost("{targetUserId:guid}/reject")]
    public Task<IActionResult> Reject(Guid targetUserId, CancellationToken cancellationToken) =>
        RecordDecision(targetUserId, true, cancellationToken);

    private async Task<IActionResult> RecordDecision(Guid targetUserId, bool rejected, CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out var userId)) return Unauthorized();
        try
        {
            await _discovery.RecordDecisionAsync(userId, targetUserId, rejected, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (DbUpdateException) { return Conflict(new { message = "Keşif kararı aynı anda kaydedildi." }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
