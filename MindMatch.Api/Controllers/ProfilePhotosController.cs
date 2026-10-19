using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Dtos.UserProfile;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Authorize]
public sealed class ProfilePhotosController : ControllerBase
{
    private readonly IProfilePhotoService _service;
    public ProfilePhotosController(IProfilePhotoService service) => _service = service;

    [HttpGet("/api/profiles/me/photos")]
    public Task<IActionResult> GetMine(CancellationToken cancellationToken) => Get(GetUserId(), cancellationToken);

    [HttpGet("/api/profiles/{userId:guid}/photos")]
    public Task<IActionResult> GetUser(Guid userId, CancellationToken cancellationToken) => Get(userId, cancellationToken);

    [HttpPost("/api/profiles/me/photos")]
    [EnableRateLimiting("media-upload")]
    public async Task<IActionResult> Add(AddProfilePhotoRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await _service.AddAsync(userId.Value, request.MediaAssetId, cancellationToken)); }
        catch (DbUpdateException) { return Conflict(new { message = "Profil fotoğrafı eşzamanlı bir değişiklik nedeniyle eklenemedi." }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("/api/profiles/me/photos/{photoId:guid}/primary")]
    public async Task<IActionResult> SetPrimary(Guid photoId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { await _service.SetPrimaryAsync(userId.Value, photoId, cancellationToken); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("/api/profiles/me/photos/{photoId:guid}")]
    public async Task<IActionResult> Delete(Guid photoId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { await _service.DeleteAsync(userId.Value, photoId, cancellationToken); return NoContent(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    private Task<IActionResult> Get(Guid? owner, CancellationToken cancellationToken)
    {
        var viewer = GetUserId();
        if (viewer is null || owner is null) return Task.FromResult<IActionResult>(Unauthorized());
        return GetCore(viewer.Value, owner.Value, cancellationToken);
    }

    private async Task<IActionResult> GetCore(Guid viewer, Guid owner, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetAsync(viewer, owner, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public sealed class AddProfilePhotoRequest
{
    public Guid MediaAssetId { get; set; }
}
