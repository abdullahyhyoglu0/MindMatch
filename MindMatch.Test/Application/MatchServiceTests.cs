using MindMatch.Application.Dtos.Notifications;
using MindMatch.Application.Dtos.Premium;
using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class MatchServiceTests
{
    [Fact]
    public async Task RejectRequest_DoesNotCreateConversation()
    {
        var fixture = new Fixture();
        var requester = fixture.AddUser();
        var receiver = fixture.AddUser();
        var response = await fixture.Service.SendRequestAsync(requester.Id, receiver.Id);

        await fixture.Service.RejectRequestAsync(response.Id, receiver.Id);

        Assert.Equal(MatchStatus.Rejected, fixture.Matches.Single().Status);
        Assert.Empty(fixture.Conversations);
        Assert.Contains(fixture.Outbox, item => item.EventName == "MatchRejected");
    }

    [Fact]
    public async Task AcceptRequest_CreatesOneConversationWithFixedPremiumRetention()
    {
        var fixture = new Fixture { RetentionDays = 7 };
        var requester = fixture.AddUser();
        var receiver = fixture.AddUser();
        var response = await fixture.Service.SendRequestAsync(requester.Id, receiver.Id);

        await fixture.Service.AcceptRequestAsync(response.Id, receiver.Id);

        var conversation = Assert.Single(fixture.Conversations);
        Assert.Equal(7, conversation.MessageRetentionDays);
        Assert.Equal(conversation.Id, fixture.Matches.Single().ConversationId);
        Assert.Equal(3, fixture.Matches.Single().Version);
        Assert.Contains(fixture.Outbox, item => item.EventName == "MatchAccepted");
    }

    [Fact]
    public async Task EndMatch_DeactivatesLinkedConversation()
    {
        var fixture = new Fixture();
        var requester = fixture.AddUser();
        var receiver = fixture.AddUser();
        var response = await fixture.Service.SendRequestAsync(requester.Id, receiver.Id);
        await fixture.Service.AcceptRequestAsync(response.Id, receiver.Id);
        var conversation = Assert.Single(fixture.Conversations);
        var conversationUpdatesBeforeEnd = fixture.Outbox.Count(item => item.EventName == "ConversationUpdated");

        await fixture.Service.EndMatchAsync(response.Id, requester.Id);

        Assert.False(conversation.IsActive);
        Assert.Equal(conversationUpdatesBeforeEnd + 2,
            fixture.Outbox.Count(item => item.EventName == "ConversationUpdated"));
    }

    [Fact]
    public async Task AcceptRequest_RejectsWhenRequesterWasDeactivated()
    {
        var fixture = new Fixture();
        var requester = fixture.AddUser();
        var receiver = fixture.AddUser();
        var response = await fixture.Service.SendRequestAsync(requester.Id, receiver.Id);
        requester.Deactivate();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.AcceptRequestAsync(response.Id, receiver.Id));

        Assert.Empty(fixture.Conversations);
        Assert.Equal(MatchStatus.Pending, fixture.Matches.Single().Status);
    }

    private sealed class Fixture
    {
        public readonly List<Match> Matches = [];
        public readonly List<Conversation> Conversations = [];
        public readonly List<RealtimeOutboxMessage> Outbox = [];
        private readonly Dictionary<Guid, User> _users = [];
        public int RetentionDays { get; init; } = 1;
        public MatchService Service { get; }

        public Fixture()
        {
            Service = new MatchService(new MatchRepo(this), new ConversationsRepo(this), new Blocks(),
                new MessageRequests(), new Users(this), new Premium(this), new OutboxRepo(this),
                new Notifications(), new UnitOfWork());
        }

        public User AddUser()
        {
            var user = new User($"{Guid.NewGuid():N}@test.invalid", "hash");
            _users.Add(user.Id, user);
            return user;
        }

        private sealed class MatchRepo(Fixture f) : IMatchRepository
        {
            public Task<Match?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Matches.SingleOrDefault(x => x.Id == id));
            public Task<bool> HasPendingOrAcceptedMatchAsync(Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(f.Matches.Any(x => x.Status is MatchStatus.Pending or MatchStatus.Accepted && ((x.RequesterId == a && x.ReceiverId == b) || (x.RequesterId == b && x.ReceiverId == a))));
            public Task<bool> HasAcceptedMatchAsync(Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(false);
            public Task<List<Match>> GetByUserIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Matches.Where(x => x.RequesterId == id || x.ReceiverId == id).ToList());
            public Task<List<Match>> GetIncomingPendingAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Matches.Where(x => x.ReceiverId == id && x.Status == MatchStatus.Pending).ToList());
            public Task AddAsync(Match match, CancellationToken ct = default) { f.Matches.Add(match); return Task.CompletedTask; }
            public void Update(Match match) { }
        }

        private sealed class ConversationsRepo(Fixture f) : IConversationRepository
        {
            public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Conversations.SingleOrDefault(x => x.Id == id));
            public Task<Conversation?> GetByUsersAsync(Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(f.Conversations.SingleOrDefault(x => (x.User1Id == a && x.User2Id == b) || (x.User1Id == b && x.User2Id == a)));
            public Task<List<Conversation>> GetByUserIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Conversations.Where(x => x.User1Id == id || x.User2Id == id).ToList());
            public Task AddAsync(Conversation conversation, CancellationToken ct = default) { f.Conversations.Add(conversation); return Task.CompletedTask; }
        }

        private sealed class Blocks : IUserBlockRepository
        {
            public Task<bool> IsBlockedEitherWayAsync(Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(false);
            public Task<bool> IsBlockedByAsync(Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(false);
            public Task<List<Guid>> GetBlockedUserIdsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
            public Task<List<Guid>> GetRelatedUserIdsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
            public Task AddAsync(UserBlock block, CancellationToken ct = default) => Task.CompletedTask;
            public Task RemoveAsync(Guid a, Guid b, CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class MessageRequests : IMessageRequestRepository
        {
            public Task<MessageRequest?> GetByConversationIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<MessageRequest?>(null);
            public Task<List<MessageRequest>> GetIncomingAsync(Guid id, CancellationToken ct = default) => Task.FromResult(new List<MessageRequest>());
            public Task AddAsync(MessageRequest request, CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class Users(Fixture f) : IUserRepository
        {
            public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(f._users.Values.SingleOrDefault(x => x.Email == email));
            public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f._users.GetValueOrDefault(id));
            public Task AddAsync(User user, CancellationToken ct = default) { f._users[user.Id] = user; return Task.CompletedTask; }
            public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => Task.FromResult(f._users.Values.Any(x => x.Email == email));
            public Task<List<User>> GetExpiredAdminFrozenBatchAsync(DateTime nowUtc, int take, CancellationToken ct = default) => Task.FromResult(new List<User>());
            public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class Premium(Fixture f) : IPremiumService
        {
            public Task<bool> IsPremiumAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.RetentionDays == 7);
            public Task<int> GetConversationRetentionDaysAsync(Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(f.RetentionDays);
            public Task<PremiumStatusResponse> GetStatusAsync(Guid id, CancellationToken ct = default) => Task.FromResult(new PremiumStatusResponse());
        }

        private sealed class OutboxRepo(Fixture f) : IRealtimeOutboxRepository
        {
            public Task AddAsync(RealtimeOutboxMessage item, CancellationToken ct = default) { f.Outbox.Add(item); return Task.CompletedTask; }
            public Task<List<RealtimeOutboxMessage>> GetPendingBatchAsync(DateTime now, int take, CancellationToken ct = default) => Task.FromResult(new List<RealtimeOutboxMessage>());
            public Task DeleteExpiredBatchAsync(DateTime now, int take, CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class Notifications : INotificationService
        {
            public Task CreateAsync(Guid id, NotificationType type, string title, string body, Guid? referenceId, CancellationToken ct = default) => Task.CompletedTask;
            public Task QueueAsync(Guid id, NotificationType type, string title, string body, Guid? referenceId, CancellationToken ct = default) => Task.CompletedTask;
            public Task<NotificationPageResponse> GetMineAsync(Guid id, int pageSize, DateTime? before, Guid? beforeId, CancellationToken ct = default) => Task.FromResult(new NotificationPageResponse());
            public Task MarkReadAsync(Guid id, Guid notificationId, CancellationToken ct = default) => Task.CompletedTask;
            public Task MarkAllReadAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class UnitOfWork : IUnitOfWork
        {
            public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
        }
    }
}
