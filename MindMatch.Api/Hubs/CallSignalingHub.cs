using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MindMatch.Application.Interfaces;
using MindMatch.Api.Services;

namespace MindMatch.Api.Hubs;

[Authorize]
public sealed class CallSignalingHub : Hub
{
    private readonly ICallService _calls;
    private readonly RedisPresenceTracker _presence;
    private readonly RedisRateLimiter _rateLimiter;
    public CallSignalingHub(ICallService calls, RedisPresenceTracker presence, RedisRateLimiter rateLimiter)
    { _calls = calls; _presence = presence; _rateLimiter = rateLimiter; }

    public override async Task OnConnectedAsync()
    {
        await _presence.ConnectedAsync(GetUserId(), Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId,
            MessagesHub.GetUserGroup(GetUserId()), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _presence.DisconnectedAsync(GetUserId(), Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public Task SendOffer(Guid callId, string offer) => SendSignal(callId, "CallOffer", offer, callerOnly: true);
    public Task SendAnswer(Guid callId, string answer) => SendSignal(callId, "CallAnswer", answer, receiverOnly: true);
    public Task SendIceCandidate(Guid callId, string candidate) => SendSignal(callId, "CallIceCandidate", candidate);

    private async Task SendSignal(Guid callId, string eventName, string payload, bool callerOnly = false, bool receiverOnly = false)
    {
        var userId = GetUserId();
        var limit = await _rateLimiter.TryConsumeAsync("call-signaling", userId.ToString("N"));
        if (!limit.Allowed) throw new HubException($"Sinyalleşme sınırı aşıldı. {limit.RetryAfterSeconds} saniye sonra tekrar deneyin.");
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 32_000)
            throw new HubException("Sinyalleşme verisi geçersiz veya çok büyük.");
        MindMatch.Application.Dtos.Calls.CallResponse call;
        try { call = await _calls.GetForSignalingAsync(callId, userId, Context.ConnectionAborted); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException)
        { throw new HubException(ex.Message); }

        if ((callerOnly && call.CallerId != userId) || (receiverOnly && call.ReceiverId != userId))
            throw new HubException("Bu sinyalleşme işlemi için yetkiniz yok.");

        var recipientId = call.CallerId == userId ? call.ReceiverId : call.CallerId;
        await Clients.Group(MessagesHub.GetUserGroup(recipientId))
            .SendAsync(eventName, new { callId, senderId = userId, payload }, Context.ConnectionAborted);
    }

    private Guid GetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(value, out var id)) throw new HubException("Kimlik doğrulaması gerekli.");
        return id;
    }
}
