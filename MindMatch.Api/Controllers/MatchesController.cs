
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MindMatch.Application.Dtos.Match;
using MindMatch.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatchesController : ControllerBase
{
    private readonly IMatchService _matchService;

    public MatchesController(IMatchService matchService) => _matchService = matchService;

    // Eşleşme isteği gönder
    [HttpPost("{receiverId:guid}")]
    [EnableRateLimiting("match-write")]
    public async Task<ActionResult<MatchResponse>> SendRequest(
        Guid receiverId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        MatchResponse result;
        try
        {
            result = await _matchService.SendRequestAsync(
                userId.Value, receiverId, cancellationToken);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DbUpdateException) { return Conflict(new { message = "Bu kullanıcıyla etkin bir eşleşme isteği zaten var." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        return Ok(result);
    }

    [HttpPost]
    [EnableRateLimiting("match-write")]
    public async Task<ActionResult<MatchResponse>> CreateRequest(
        SendMatchRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await _matchService.SendRequestAsync(userId.Value, request.ReceiverId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DbUpdateException) { return Conflict(new { message = "Bu kullanıcıyla etkin bir eşleşme isteği zaten var." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // Gelen eşleşme isteğini kabul et
    [HttpPost("{matchId:guid}/accept")]
    [EnableRateLimiting("match-write")]
    public async Task<ActionResult<MatchResponse>> AcceptRequest(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        MatchResponse result;
        try
        {
            result = await _matchService.AcceptRequestAsync(
                matchId, userId.Value, cancellationToken);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Eşleşme aynı anda başka bir işlem tarafından yanıtlandı." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }

        return Ok(result);
    }

    // Gelen eşleşme isteğini reddet
    [HttpPost("{matchId:guid}/reject")]
    [EnableRateLimiting("match-write")]
    public async Task<ActionResult<MatchResponse>> RejectRequest(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try { return Ok(await _matchService.RejectRequestAsync(matchId, userId.Value, cancellationToken)); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Eşleşme aynı anda başka bir işlem tarafından yanıtlandı." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // Kabul edilmiş eşleşmeyi bitir
    [HttpPost("{matchId:guid}/end")]
    [EnableRateLimiting("match-write")]
    public async Task<ActionResult<MatchResponse>> EndMatch(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try { return Ok(await _matchService.EndMatchAsync(matchId, userId.Value, cancellationToken)); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Eşleşme aynı anda başka bir işlem tarafından değiştirildi." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // Kullanıcının tüm eşleşmelerini getir
    [HttpGet("me")]
    [HttpGet]
    public async Task<ActionResult<List<MatchResponse>>> GetMyMatches(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var result = await _matchService.GetMyMatchesAsync(
            userId.Value,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<MatchResponse>>> GetRequests(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _matchService.GetIncomingRequestsAsync(userId.Value, cancellationToken));
    }

    private Guid? GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }
}
