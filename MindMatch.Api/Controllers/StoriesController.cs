using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using MindMatch.Application.Dtos.Stories;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/stories")]
[Authorize]
public sealed class StoriesController : ControllerBase
{
    private readonly IStoryService _service;
    public StoriesController(IStoryService service) => _service = service;

    [HttpPost]
    [EnableRateLimiting("media-upload")]
    public async Task<IActionResult> Create(CreateStoryRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var story = await _service.CreateAsync(userId.Value, request, cancellationToken);
            return CreatedAtAction(nameof(GetStory), new { storyId = story.Id }, story);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await _service.GetFeedAsync(userId.Value, take, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await _service.GetUserStoriesAsync(userId.Value, userId.Value, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("{storyId:guid}", Name = "GetStory")]
    public async Task<IActionResult> GetStory(Guid storyId, CancellationToken cancellationToken)
    {
        var viewerId = GetUserId();
        if (viewerId is null) return Unauthorized();
        try { return Ok(await _service.GetAsync(viewerId.Value, storyId, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetUserStories(Guid userId, CancellationToken cancellationToken)
    {
        var viewerId = GetUserId();
        if (viewerId is null) return Unauthorized();
        try { return Ok(await _service.GetUserStoriesAsync(viewerId.Value, userId, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{storyId:guid}/view")]
    [HttpPost("{storyId:guid}/views")]
    [EnableRateLimiting("hub-events")]
    public async Task<IActionResult> View(Guid storyId, CancellationToken cancellationToken)
    {
        var viewerId = GetUserId();
        if (viewerId is null) return Unauthorized();
        try { await _service.ViewAsync(viewerId.Value, storyId, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{storyId:guid}/viewers")]
    public async Task<IActionResult> GetViewers(Guid storyId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await _service.GetViewersAsync(userId.Value, storyId, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{storyId:guid}")]
    public async Task<IActionResult> Delete(Guid storyId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try { await _service.DeleteAsync(userId.Value, storyId, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
