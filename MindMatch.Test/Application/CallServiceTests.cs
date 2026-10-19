using System.Reflection;
using MindMatch.Application.Dtos.Notifications;
using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class CallServiceTests
{
    [Fact]
    public async Task Start_RejectsBlockedParticipantWithoutCreatingCall()
    {
        var fixture = new Fixture { IsBlocked = true };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.StartAsync(fixture.Caller.Id, fixture.Conversation.Id, CallType.Audio));

        Assert.Null(fixture.SavedCall);
        Assert.Empty(fixture.Outbox);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task Start_RejectsUserWhoIsNotConversationParticipant()
    {
        var fixture = new Fixture();
        var outsider = new User("outsider@test.invalid", "hash");
        fixture.Users[outsider.Id] = outsider;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.StartAsync(outsider.Id, fixture.Conversation.Id, CallType.Video));

        Assert.Null(fixture.SavedCall);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task Answer_OnlyReceiverCanAcceptCall()
    {
        var fixture = new Fixture();
        fixture.ExistingCall = new Call(fixture.Conversation.Id, fixture.Caller.Id,
            fixture.Receiver.Id, CallType.Audio);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.AnswerAsync(fixture.ExistingCall.Id, fixture.Caller.Id));

        Assert.Equal(CallStatus.Ringing, fixture.ExistingCall.Status);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task Start_QueuesIncomingCallAndNotificationWhenAllowed()
    {
        var fixture = new Fixture();
        fixture.ReceiverProfile.UpdatePreferences(true, true, true, true, true, true);

        var response = await fixture.Service.StartAsync(fixture.Caller.Id,
            fixture.Conversation.Id, CallType.Video);

        Assert.Equal(CallStatus.Ringing, response.Status);
        Assert.Equal(fixture.Receiver.Id, response.ReceiverId);
        Assert.Equal(fixture.SavedCall!.Id, response.Id);
        Assert.Contains(fixture.Outbox, item => item.EventName == "CallIncoming" &&
            item.DestinationId == fixture.Receiver.Id);
        Assert.Equal(1, fixture.NotificationCount);
        Assert.Equal(1, fixture.SaveCount);
    }

    private sealed class Fixture
    {
        public User Caller { get; } = new("caller@test.invalid", "hash");
        public User Receiver { get; } = new("receiver@test.invalid", "hash");
        public Conversation Conversation { get; }
        public UserProfile ReceiverProfile { get; }
        public Dictionary<Guid, User> Users { get; } = [];
        public bool IsBlocked { get; init; }
        public Call? ExistingCall { get; set; }
        public Call? SavedCall { get; private set; }
        public List<RealtimeOutboxMessage> Outbox { get; } = [];
        public int NotificationCount { get; private set; }
        public int SaveCount { get; private set; }
        public CallService Service { get; }

        public Fixture()
        {
            Conversation = new Conversation(Caller.Id, Receiver.Id);
            ReceiverProfile = new UserProfile(Receiver.Id, "Receiver");
            Users[Caller.Id] = Caller;
            Users[Receiver.Id] = Receiver;
            Service = new CallService(
                Proxy<ICallRepository>((method, args) => method.Name switch
                {
                    nameof(ICallRepository.GetByIdAsync) => Task.FromResult(ExistingCall),
                    nameof(ICallRepository.AddAsync) => CaptureCall(args),
                    nameof(ICallRepository.GetByConversationIdAsync) => Task.FromResult(new List<Call>()),
                    nameof(ICallRepository.GetMissedBatchAsync) => Task.FromResult(new List<Call>()),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IConversationRepository>((method, _) => method.Name switch
                {
                    nameof(IConversationRepository.GetByIdAsync) => Task.FromResult<Conversation?>(Conversation),
                    nameof(IConversationRepository.GetByUsersAsync) => Task.FromResult<Conversation?>(null),
                    nameof(IConversationRepository.GetByUserIdAsync) => Task.FromResult(new List<Conversation>()),
                    nameof(IConversationRepository.AddAsync) => Task.CompletedTask,
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IMatchRepository>((method, _) => method.Name switch
                {
                    nameof(IMatchRepository.HasAcceptedMatchAsync) => Task.FromResult(false),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUserProfileRepository>((method, args) => method.Name switch
                {
                    nameof(IUserProfileRepository.GetByUserIdAsync) when (Guid)args![0]! == Receiver.Id =>
                        Task.FromResult<UserProfile?>(ReceiverProfile),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IMessageRequestRepository>((method, _) => method.Name switch
                {
                    nameof(IMessageRequestRepository.GetByConversationIdAsync) => Task.FromResult<MessageRequest?>(null),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUserBlockRepository>((method, _) => method.Name switch
                {
                    nameof(IUserBlockRepository.IsBlockedEitherWayAsync) => Task.FromResult(IsBlocked),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUserRepository>((method, args) => method.Name switch
                {
                    nameof(IUserRepository.GetByIdAsync) => Task.FromResult(Users.GetValueOrDefault((Guid)args![0]!)),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IRealtimeOutboxRepository>((method, args) => method.Name switch
                {
                    nameof(IRealtimeOutboxRepository.AddAsync) => CaptureOutbox(args),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<INotificationService>((method, _) => method.Name switch
                {
                    nameof(INotificationService.QueueAsync) => QueueNotification(),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUnitOfWork>((method, _) => method.Name switch
                {
                    nameof(IUnitOfWork.SaveChangesAsync) => SaveChanges(),
                    _ => throw new NotSupportedException(method.Name)
                }));
        }

        private Task CaptureCall(object?[]? args)
        {
            SavedCall = Assert.IsType<Call>(args![0]);
            return Task.CompletedTask;
        }

        private Task CaptureOutbox(object?[]? args)
        {
            Outbox.Add(Assert.IsType<RealtimeOutboxMessage>(args![0]));
            return Task.CompletedTask;
        }

        private Task QueueNotification()
        {
            NotificationCount++;
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
