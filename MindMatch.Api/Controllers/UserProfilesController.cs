
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Dtos.UserProfile;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/profiles")]
[Authorize]
public class UserProfilesController : ControllerBase
{
    private readonly IUserProfileService _profileService;

    public UserProfilesController(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetMyProfile(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var profile = await _profileService.GetMyProfileAsync(
            userId.Value, cancellationToken);

        return Ok(profile);
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetPublicProfile(Guid userId, CancellationToken cancellationToken)
    {
        var viewerId = GetCurrentUserId();
        if (viewerId is null) return Unauthorized();
        try { return Ok(await _profileService.GetPublicProfileAsync(viewerId.Value, userId, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserProfileResponse>> UpdateMyProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var profile = await _profileService.UpdateMyProfileAsync(userId.Value, request, cancellationToken);
            return Ok(profile);
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { message = "Bu kullanıcı adı zaten kullanılıyor." }); }
    }

    [HttpPatch("me/visibility")]
    public async Task<IActionResult> SetVisibility(
        [FromQuery] bool isVisible,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        await _profileService.SetProfileVisibilityAsync(
            userId.Value, isVisible, cancellationToken);

        return NoContent();
    }

    [HttpPut("me/preferences")]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] ProfilePreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        await _profileService.UpdatePreferencesAsync(userId.Value, request, cancellationToken);
        return NoContent();
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
