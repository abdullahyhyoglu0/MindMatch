
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;

    public ConversationsController(
        IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    // GET: api/Conversations
    [HttpGet]
    public async Task<IActionResult> GetMyConversations(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var conversations =
            await _conversationService.GetMyConversationsAsync(
                userId.Value,
                cancellationToken);

        return Ok(conversations);
    }

    [HttpGet("{conversationId:guid}")]
    public async Task<IActionResult> Get(Guid conversationId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await _conversationService.GetAsync(conversationId, userId.Value, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("{conversationId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid conversationId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try { await _conversationService.RestoreConversationAsync(conversationId, userId.Value, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // DELETE: api/Conversations/{conversationId}
    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> HideConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            await _conversationService.HideConversationAsync(
                conversationId,
                userId.Value,
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }
}
