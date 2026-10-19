using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Dtos.Message;
using MindMatch.Application.Interfaces;
using MindMatch.Api.Services;

namespace MindMatch.Api.Hubs;

[Authorize]
public sealed class MessagesHub : Hub
{
    private readonly IConversationRepository _conversations;
    private readonly IMessageRequestRepository _messageRequests;
    private readonly IMessageService _messages;
    private readonly RedisPresenceTracker _presence;
    private readonly RedisRateLimiter _rateLimiter;
    private readonly IUserRepository _users;
    private readonly IUserBlockRepository _userBlocks;

    public MessagesHub(
        IConversationRepository conversations,
        IMessageRequestRepository messageRequests,
        IMessageService messages,
        RedisPresenceTracker presence,
        RedisRateLimiter rateLimiter,
        IUserRepository users,
        IUserBlockRepository userBlocks)
    {
        _conversations = conversations;
        _messageRequests = messageRequests;
        _messages = messages;
        _presence = presence;
        _rateLimiter = rateLimiter;
        _users = users;
        _userBlocks = userBlocks;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await EnsureUserActiveAsync(userId);
        await _presence.ConnectedAsync(userId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _presence.DisconnectedAsync(GetUserId(), Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetUserId();
        await EnsureUserActiveAsync(userId);
        var conversation = await _conversations.GetByIdAsync(
            conversationId, Context.ConnectionAborted);

        if (conversation is null)
            throw new HubException("Sohbet bulunamadı.");

        if (conversation.User1Id != userId && conversation.User2Id != userId)
            throw new HubException("Bu sohbete erişim yetkiniz yok.");
        if (!conversation.IsActive)
            throw new HubException("Bu sohbet artık etkin değil.");
        await EnsureConversationMessagingAllowedAsync(conversationId);
        await EnsureUsersAreNotBlockedAsync(userId, conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetConversationGroup(conversationId),
            Context.ConnectionAborted);
    }

    public Task LeaveConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetConversationGroup(conversationId),
            Context.ConnectionAborted);

    public async Task SendMessage(SendMessageRequest request)
    {
        var userId = GetUserId();
        var limit = await _rateLimiter.TryConsumeAsync("message-write", userId.ToString("N"));
        if (!limit.Allowed) throw new HubException($"İstek sınırı aşıldı. {limit.RetryAfterSeconds} saniye sonra tekrar deneyin.");
        await EnsureUserActiveAsync(userId);
        try
        {
            await _messages.SendAsync(userId, request, Context.ConnectionAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new HubException("Medya eşzamanlı olarak başka bir konuşmaya bağlandı. Yeniden deneyin.");
        }
    }

    public async Task SendTyping(Guid conversationId, bool isTyping)
    {
        var userId = GetUserId();
        var limit = await _rateLimiter.TryConsumeAsync("hub-events", userId.ToString("N"));
        if (!limit.Allowed) throw new HubException($"İstek sınırı aşıldı. {limit.RetryAfterSeconds} saniye sonra tekrar deneyin.");
        await EnsureUserActiveAsync(userId);
        var conversation = await _conversations.GetByIdAsync(
            conversationId, Context.ConnectionAborted);

        if (conversation is null || !conversation.IsActive ||
            (conversation.User1Id != userId && conversation.User2Id != userId))
            throw new HubException("Bu sohbete erişim yetkiniz yok.");
        await EnsureConversationMessagingAllowedAsync(conversationId);

        var recipientId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
        await EnsureUsersAreNotBlockedAsync(userId, recipientId);
        var recipient = await _users.GetByIdAsync(recipientId, Context.ConnectionAborted);
        if (recipient is null || !recipient.IsActive) return;
        await Clients.Group(GetUserGroup(recipientId))
            .SendAsync("TypingChanged", new { conversationId, userId, isTyping },
                Context.ConnectionAborted);
    }

    public static string GetConversationGroup(Guid conversationId) =>
        $"conversation:{conversationId:N}";

    public static string GetUserGroup(Guid userId) => $"user:{userId:N}";

    private async Task EnsureConversationMessagingAllowedAsync(Guid conversationId)
    {
        var request = await _messageRequests.GetByConversationIdAsync(conversationId, Context.ConnectionAborted);
        if (request is not null && request.Status != Domain.Entities.MessageRequestStatus.Accepted)
            throw new HubException("Mesaj isteği kabul edilmeden sohbet açılamaz.");
    }

    private async Task EnsureUserActiveAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId, Context.ConnectionAborted);
        if (user is null || !user.IsActive)
            throw new HubException("Kullanıcı hesabı etkin değil.");
    }

    private async Task EnsureUsersAreNotBlockedAsync(Guid userId, Guid otherUserId)
    {
        if (await _userBlocks.IsBlockedEitherWayAsync(userId, otherUserId, Context.ConnectionAborted))
            throw new HubException("Engelleme nedeniyle sohbete erişilemez.");
    }

    private Guid GetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? Context.User?.FindFirstValue("sub");

        if (!Guid.TryParse(value, out var userId))
            throw new HubException("Kullanıcı kimliği doğrulanamadı.");

        return userId;
    }
}
