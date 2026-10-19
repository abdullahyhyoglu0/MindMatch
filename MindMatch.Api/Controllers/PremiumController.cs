using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/premium")]
[Authorize]
public sealed class PremiumController : ControllerBase
{
    private readonly IPremiumService _service;
    public PremiumController(IPremiumService service) => _service = service;

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out var userId)) return Unauthorized();
        return Ok(await _service.GetStatusAsync(userId, cancellationToken));
    }
}
