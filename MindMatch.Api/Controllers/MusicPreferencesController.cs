using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindMatch.Application.Dtos.UserProfile;
using MindMatch.Application.Services;

namespace MindMatch.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/preferences/music")]
public sealed class MusicPreferencesController : ControllerBase
{
    private readonly MusicPreferenceService _service;
    public MusicPreferencesController(MusicPreferenceService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId)
            ? Ok(await _service.GetAsync(userId, cancellationToken)) : Unauthorized();

    [HttpPut]
    public async Task<IActionResult> Update(MusicPreferencesRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId))
            return Unauthorized();
        try
        {
            await _service.UpdateAsync(userId, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
