using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindMatch.Application.Dtos.Moderation;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,Moderator")]
public sealed class AdminController : ControllerBase
{
    private readonly IReportService _service;
    private readonly IMediaService _media;
    public AdminController(IReportService service, IMediaService media) { _service = service; _media = media; }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports([FromQuery] ReportStatus? status, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var actor = GetUserId();
        if (actor is null) return Unauthorized();
        try { return Ok(await _service.GetQueueAsync(actor.Value, status, take, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("reports/{reportId:guid}")]
    public async Task<IActionResult> GetReport(Guid reportId, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        if (actor is null) return Unauthorized();
        try { return Ok(await _service.GetAdminDetailAsync(actor.Value, reportId, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("reports/{reportId:guid}/messages")]
    public async Task<IActionResult> GetReportMessages(Guid reportId, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        if (actor is null) return Unauthorized();
        try
        {
            var report = await _service.GetAdminDetailAsync(actor.Value, reportId, cancellationToken);
            return Ok(report.Messages);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("reports/{reportId:guid}/media/{mediaAssetId:guid}")]
    public async Task<IActionResult> GetReportMedia(Guid reportId, Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        if (actor is null) return Unauthorized();
        try
        {
            var result = await _media.OpenReportedEvidenceAsync(actor.Value, reportId,
                mediaAssetId, cancellationToken);
            if (result is null) return NotFound();
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.CacheControl = "private, no-store";
            return File(result.Value.Content, result.Value.ContentType);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("reports/{reportId:guid}/assign")]
    public async Task<IActionResult> Assign(Guid reportId, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        if (actor is null) return Unauthorized();
        try { await _service.AssignAsync(actor.Value, reportId, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("reports/{reportId:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        if (actor is null) return Unauthorized();
        try { await _service.ResolveAsync(actor.Value, reportId, request.Resolution, request.ViolationConfirmed, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("users/{userId:guid}/freeze")]
    public async Task<IActionResult> FreezeUser(Guid userId, FreezeUserRequest request, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        if (actor is null) return Unauthorized();
        try { await _service.FreezeUserAsync(actor.Value, userId, request.Reason, request.UntilUtc, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("users/{userId:guid}/activate")]
    public async Task<IActionResult> ActivateUser(Guid userId, ReasonRequest request, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        if (actor is null) return Unauthorized();
        try { await _service.ActivateUserAsync(actor.Value, userId, request.Reason, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("users/{userId:guid}/moderation-history")]
    public async Task<IActionResult> GetHistory(Guid userId, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        if (actor is null) return Unauthorized();
        try { return Ok(await _service.GetModerationHistoryAsync(actor.Value, userId, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public sealed class ResolveReportRequest
{
    public string Resolution { get; set; } = string.Empty;
    public bool ViolationConfirmed { get; set; }
}

public sealed class FreezeUserRequest
{
    public string Reason { get; set; } = string.Empty;
    public DateTime? UntilUtc { get; set; }
}

public sealed class ReasonRequest
{
    public string Reason { get; set; } = string.Empty;
}
