using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindMatch.Application.Interfaces;

namespace MindMatch.Api.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountService _service;

    public AccountController(IAccountService service) => _service = service;

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null ? Unauthorized() : Ok(await _service.GetStatusAsync(userId.Value, cancellationToken));
    }

    [HttpPost("freeze")]
    public async Task<IActionResult> Freeze(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        await _service.FreezeAsync(userId.Value, cancellationToken);
        return NoContent();
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            await _service.ActivateAsync(userId.Value, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException) { return Forbid(); }
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
