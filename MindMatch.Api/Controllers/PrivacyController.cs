using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindMatch.Application.Dtos.UserProfile;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/privacy")]
public sealed class PrivacyController : ControllerBase
{
    private readonly IUserProfileService _profiles;
    public PrivacyController(IUserProfileService profiles) => _profiles = profiles;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var profile = await _profiles.GetMyProfileAsync(userId, cancellationToken);
        return Ok(new PrivacySettingsResponse(profile.ShowReadReceipts, profile.ShowLastSeen,
            profile.ShowOnlineStatus, profile.AllowMessageRequests, profile.AllowCallsFromNonMatches,
            profile.ShowPremiumBadge, profile.PremiumPriorityOptIn));
    }

    [HttpPut]
    public async Task<IActionResult> Update(ProfilePreferencesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            await _profiles.UpdatePreferencesAsync(userId, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
