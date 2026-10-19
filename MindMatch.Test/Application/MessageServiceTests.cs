using System.Reflection;
using MindMatch.Application.Dtos.Message;
using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class MessageServiceTests
{
    [Fact]
    public async Task SendTextMessage_UsesAuthenticatedSenderAndQueuesRecipientEvents()
    {
        var fixture = new Fixture();
        var response = await fixture.Service.SendAsync(fixture.Sender.Id, new SendMessageRequest
        {
            ConversationId = fixture.Conversation.Id,
            Type = MessageType.Text,
            Content = "hello"
        });

        Assert.Equal(fixture.Sender.Id, fixture.SavedMessage!.SenderId);
        Assert.Equal(fixture.RecipientId, response.RecipientId);
        Assert.Equal("hello", response.Content);
        Assert.Equal(4, fixture.Outbox.Count);
        Assert.Equal(1, fixture.SaveCount);
    }

    [Fact]
    public async Task SendMessage_RejectsUnacceptedMessageRequestWithoutSaving()
    {
        var fixture = new Fixture();
        fixture.MessageRequest = new MessageRequest(fixture.Conversation.Id,
            fixture.Sender.Id, fixture.RecipientId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.SendAsync(
            fixture.Sender.Id, new SendMessageRequest
            {
                ConversationId = fixture.Conversation.Id,
                Type = MessageType.Text,
                Content = "hello"
            }));

        Assert.Null(fixture.SavedMessage);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task SendMessage_CannotUseMediaBoundToAnotherConversation()
    {
        var fixture = new Fixture();
        var asset = new MediaAsset(fixture.Sender.Id, MediaCategory.MessageImage,
            "message/photo.jpg", "image/jpeg", 10);
        asset.BindToConversation(Guid.NewGuid());
        fixture.Asset = asset;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.SendAsync(
            fixture.Sender.Id, new SendMessageRequest
            {
                ConversationId = fixture.Conversation.Id,
                Type = MessageType.Image,
                MediaAssetId = asset.Id
            }));

        Assert.Null(fixture.SavedMessage);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task SendMessage_RejectsInactiveRecipient()
    {
        var fixture = new Fixture();
        fixture.Recipient.Deactivate();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.SendAsync(
            fixture.Sender.Id, new SendMessageRequest
            {
                ConversationId = fixture.Conversation.Id,
                Type = MessageType.Text,
                Content = "hello"
            }));

        Assert.Null(fixture.SavedMessage);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task SendMessage_RejectsBlockedConversationWithoutSaving()
    {
        var fixture = new Fixture { IsBlocked = true };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.SendAsync(
            fixture.Sender.Id, new SendMessageRequest
            {
                ConversationId = fixture.Conversation.Id,
                Type = MessageType.Text,
                Content = "hello"
            }));

        Assert.Null(fixture.SavedMessage);
        Assert.Empty(fixture.Outbox);
        Assert.Equal(0, fixture.SaveCount);
    }

    private sealed class Fixture
    {
        public User Sender { get; } = new("sender@test.invalid", "hash");
        public User Recipient { get; } = new("recipient@test.invalid", "hash");
        public Guid RecipientId => Recipient.Id;
        public Conversation Conversation { get; }
        public MessageRequest? MessageRequest { get; set; }
        public MediaAsset? Asset { get; set; }
        public bool IsBlocked { get; init; }
        public Message? SavedMessage { get; private set; }
        public List<RealtimeOutboxMessage> Outbox { get; } = [];
        public int SaveCount { get; private set; }
        public MessageService Service { get; }

        public Fixture()
        {
            Conversation = new Conversation(Sender.Id, RecipientId);
            Service = new MessageService(
                Proxy<IMessageRepository>((method, args) => method.Name switch
                {
                    nameof(IMessageRepository.AddAsync) => CaptureMessage(args),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IConversationRepository>((method, _) => method.Name switch
                {
                    nameof(IConversationRepository.GetByIdAsync) => Task.FromResult<Conversation?>(Conversation),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUserBlockRepository>((method, _) => method.Name switch
                {
                    nameof(IUserBlockRepository.IsBlockedEitherWayAsync) => Task.FromResult(IsBlocked),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IMessageRequestRepository>((method, _) => method.Name switch
                {
                    nameof(IMessageRequestRepository.GetByConversationIdAsync) => Task.FromResult(MessageRequest),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IMediaAssetRepository>((method, _) => method.Name switch
                {
                    nameof(IMediaAssetRepository.GetByIdAsync) => Task.FromResult(Asset),
                    nameof(IMediaAssetRepository.IsUsedInMessageAsync) => Task.FromResult(false),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUserRepository>((method, args) => method.Name switch
                {
                    nameof(IUserRepository.GetByIdAsync) when (Guid)args![0]! == Sender.Id => Task.FromResult<User?>(Sender),
                    nameof(IUserRepository.GetByIdAsync) when (Guid)args![0]! == Recipient.Id => Task.FromResult<User?>(Recipient),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUserProfileRepository>((method, _) => throw new NotSupportedException(method.Name)),
                Proxy<IRealtimeOutboxRepository>((method, args) => method.Name switch
                {
                    nameof(IRealtimeOutboxRepository.AddAsync) => CaptureOutbox(args),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<INotificationService>((method, _) => method.Name switch
                {
                    nameof(INotificationService.QueueAsync) => Task.CompletedTask,
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUnitOfWork>((method, _) => method.Name switch
                {
                    nameof(IUnitOfWork.SaveChangesAsync) => SaveChanges(),
                    _ => throw new NotSupportedException(method.Name)
                }));
        }

        private Task CaptureMessage(object?[]? args)
        {
            SavedMessage = Assert.IsType<Message>(args![0]);
            return Task.CompletedTask;
        }

        private Task CaptureOutbox(object?[]? args)
        {
            Outbox.Add(Assert.IsType<RealtimeOutboxMessage>(args![0]));
            return Task.CompletedTask;
        }

        private Task<int> SaveChanges()
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var instance = DispatchProxy.Create<T, HandlerProxy<T>>();
        ((HandlerProxy<T>)(object)instance).Handler = handler;
        return instance;
    }

    private class HandlerProxy<T> : DispatchProxy where T : class
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException("Proxy method is missing."), args);
    }
}
