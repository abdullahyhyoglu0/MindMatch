using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/media")]
[Authorize]
public sealed class MediaController : ControllerBase
{
    private readonly IMediaService _service;
    public MediaController(IMediaService service) => _service = service;

    [HttpPost]
    [EnableRateLimiting("media-upload")]
    [RequestSizeLimit(52 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromQuery] MediaCategory category, IFormFile file, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { message = "Dosya boş olamaz." });
        try
        {
            await using var stream = file.OpenReadStream();
            var asset = await _service.UploadAsync(userId.Value, category, stream, file.ContentType, cancellationToken);
            return Created($"/api/media/{asset.Id}", asset);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{mediaAssetId:guid}")]
    public async Task<IActionResult> Get(Guid mediaAssetId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await _service.OpenAsync(userId.Value, mediaAssetId, cancellationToken);
        if (result is null) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpDelete("{mediaAssetId:guid}")]
    public async Task<IActionResult> Delete(Guid mediaAssetId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            return await _service.DeleteAsync(userId.Value, mediaAssetId, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Medya eşzamanlı olarak kullanıma bağlandı. Yeniden deneyin." });
        }
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
