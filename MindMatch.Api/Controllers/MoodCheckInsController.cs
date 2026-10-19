
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindMatch.Application.Dtos.MoodCheckIn;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MoodCheckInsController : ControllerBase
{
    private readonly IMoodCheckInService _moodCheckInService;

    public MoodCheckInsController(
        IMoodCheckInService moodCheckInService)
    {
        _moodCheckInService = moodCheckInService;
    }

    [HttpPost]
    public async Task<ActionResult<MoodCheckInResponse>> Create(
        [FromBody] CreateMoodCheckInRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var result = await _moodCheckInService.CreateAsync(
            userId.Value,
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("me/latest")]
    public async Task<ActionResult<MoodCheckInResponse>> GetLatest(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var result = await _moodCheckInService.GetMyLatestAsync(
            userId.Value,
            cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
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
