using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using MindMatch.Application.Dtos.Calls;
using MindMatch.Api.Services;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/calls")]
[Authorize]
public sealed class CallsController : ControllerBase
{
    private readonly ICallService _service;
    private readonly TurnCredentialService _turnCredentials;
    public CallsController(ICallService service, TurnCredentialService turnCredentials)
    { _service = service; _turnCredentials = turnCredentials; }

    [HttpGet("turn-credentials")]
    public IActionResult GetTurnCredentials()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(_turnCredentials.Create(userId.Value)); }
        catch (InvalidOperationException ex) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message }); }
    }

    [HttpPost]
    [EnableRateLimiting("call-signaling")]
    public async Task<IActionResult> Start(StartCallRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var call = await _service.StartAsync(userId.Value, request.ConversationId, request.Type, cancellationToken);
            return Created($"/api/calls/{call.Id}", call);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{callId:guid}")]
    public async Task<IActionResult> GetStatus(Guid callId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await _service.GetStatusAsync(callId, userId.Value, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("{callId:guid}/answer")]
    [HttpPost("{callId:guid}/accept")]
    [EnableRateLimiting("call-signaling")]
    public Task<IActionResult> Answer(Guid callId, CancellationToken cancellationToken) => Change(callId, _service.AnswerAsync, cancellationToken);

    [HttpPost("{callId:guid}/reject")]
    [EnableRateLimiting("call-signaling")]
    public Task<IActionResult> Reject(Guid callId, CancellationToken cancellationToken) => Change(callId, _service.RejectAsync, cancellationToken);

    [HttpPost("{callId:guid}/end")]
    [EnableRateLimiting("call-signaling")]
    public Task<IActionResult> End(Guid callId, CancellationToken cancellationToken) => Change(callId, _service.EndAsync, cancellationToken);

    [HttpGet("/api/conversations/{conversationId:guid}/calls")]
    public async Task<IActionResult> GetConversationCalls(Guid conversationId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await _service.GetByConversationAsync(conversationId, userId.Value, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private async Task<IActionResult> Change(Guid callId,
        Func<Guid, Guid, CancellationToken, Task<CallResponse>> operation,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var call = await operation(callId, userId.Value, cancellationToken);
            return Ok(call);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Arama durumu başka bir işlem tarafından değiştirildi." }); }
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public sealed class StartCallRequest
{
    public Guid ConversationId { get; set; }
    public CallType Type { get; set; }
}
