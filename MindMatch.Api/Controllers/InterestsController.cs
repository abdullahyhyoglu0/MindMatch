using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MindMatch.Application.Dtos.Interest;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InterestsController : ControllerBase
{
    private readonly IInterestService _interestService;
    private readonly IUserProfileService _profiles;

    public InterestsController(IInterestService interestService, IUserProfileService profiles)
    {
        _interestService = interestService;
        _profiles = profiles;
    }

    /// <summary>
    /// Sistemdeki tüm ilgi alanlarını listeler.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<InterestResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<InterestResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var interests = await _interestService.GetAllAsync(
            cancellationToken);

        return Ok(interests);
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMine(UpdateUserInterestsRequest request, CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out var userId)) return Unauthorized();
        try
        {
            await _profiles.UpdateInterestsAsync(userId, request.InterestIds, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
