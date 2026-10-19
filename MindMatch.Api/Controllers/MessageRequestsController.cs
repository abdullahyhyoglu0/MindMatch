using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Dtos.Message;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/message-requests")]
[Authorize]
public sealed class MessageRequestsController : ControllerBase
{
    private readonly IMessageRequestService _service;

    public MessageRequestsController(IMessageRequestService service) => _service = service;

    [HttpPost]
    [EnableRateLimiting("message-write")]
    public async Task<IActionResult> Create(CreateMessageRequest request, CancellationToken cancellationToken)
    {
        var senderId = GetUserId();
        if (senderId is null) return Unauthorized();
        try
        {
            var message = await _service.CreateAsync(senderId.Value, request, cancellationToken);
            return Created($"/api/conversations/{message.ConversationId}/messages", message);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Mesaj isteği eşzamanlı başka bir işlem nedeniyle kaydedilemedi." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "Bu kullanıcıyla mesaj isteği veya sohbet zaten var." }); }
    }

    [HttpGet("incoming")]
    public async Task<IActionResult> GetIncoming(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null ? Unauthorized() : Ok(await _service.GetIncomingAsync(userId.Value, cancellationToken));
    }

    [HttpPost("{conversationId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid conversationId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { await _service.AcceptAsync(conversationId, userId.Value, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Mesaj isteği aynı anda başka bir işlem tarafından yanıtlandı." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{conversationId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid conversationId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { await _service.RejectAsync(conversationId, userId.Value, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Mesaj isteği aynı anda başka bir işlem tarafından yanıtlandı." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
