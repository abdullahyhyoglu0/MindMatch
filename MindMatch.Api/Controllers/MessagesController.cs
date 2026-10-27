
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Dtos.Message;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService) => _messageService = messageService;

    // Mesaj gönder
    [HttpPost]
    [EnableRateLimiting("message-write")]
    public async Task<IActionResult> Send(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var message = await _messageService.SendAsync(
                userId.Value,
                request,
                cancellationToken);

            return Created($"/api/conversations/{message.ConversationId}/messages", message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Medya eşzamanlı olarak başka bir konuşmaya bağlandı. Yeniden deneyin." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Mesaj veya medya eşzamanlı bir işlem nedeniyle kaydedilemedi. Konuşmayı yenileyip gerekirse yeniden deneyin." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Sohbet geçmişini getir
    [HttpGet("conversation/{conversationId:guid}")]
    [HttpGet("/api/conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetHistory(
        Guid conversationId,
        [FromQuery] DateTime? beforeCreatedAtUtc,
        [FromQuery] Guid? beforeMessageId,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var messages = await _messageService.GetHistoryPageAsync(
                conversationId,
                userId.Value,
                beforeCreatedAtUtc,
                beforeMessageId,
                pageSize,
                cancellationToken);

            return Ok(messages);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{messageId:guid}/me")]
    public async Task<IActionResult> DeleteForMe(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await _messageService.DeleteForMeAsync(messageId, userId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{messageId:guid}/everyone")]
    public async Task<IActionResult> DeleteForEveryone(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var deleted = await _messageService.DeleteForEveryoneAsync(
                messageId, userId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{messageId:guid}/delivered")]
    public async Task<IActionResult> MarkDelivered(Guid messageId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            await _messageService.MarkDeliveredAsync(messageId, userId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{messageId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid messageId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            await _messageService.MarkReadAsync(messageId, userId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
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
