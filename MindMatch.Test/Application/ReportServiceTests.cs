using System.Reflection;
using MindMatch.Application.Dtos.Moderation;
using MindMatch.Application.Interfaces;
using MindMatch.Application.Services;
using MindMatch.Domain.Entities;

namespace MindMatch.Test.Application;

public sealed class ReportServiceTests
{
    [Fact]
    public async Task Submit_RejectsReporterOutsideSelectedConversation()
    {
        var fixture = new Fixture();
        var outsider = new User("outsider@test.invalid", "hash");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.SubmitAsync(
            outsider.Id, new CreateReportRequest
            {
                ReportedUserId = fixture.Reported.Id,
                Reason = ReportReason.Harassment,
                ConversationId = fixture.Conversation.Id
            }));

        Assert.Null(fixture.SavedReport);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task Submit_RejectsEvidenceSentBySomeoneOtherThanReportedUser()
    {
        var fixture = new Fixture();
        var message = new Message(fixture.Conversation.Id, fixture.Reporter.Id,
            MessageType.Text, "not evidence from the reported user");

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SubmitAsync(
            fixture.Reporter.Id, new CreateReportRequest
            {
                ReportedUserId = fixture.Reported.Id,
                Reason = ReportReason.Harassment,
                ConversationId = fixture.Conversation.Id,
                MessageIds = [message.Id]
            }));

        Assert.Null(fixture.SavedReport);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task Submit_RejectsInactiveReporter()
    {
        var fixture = new Fixture();
        fixture.Reporter.Deactivate();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.SubmitAsync(
            fixture.Reporter.Id, new CreateReportRequest
            {
                ReportedUserId = fixture.Reported.Id,
                Reason = ReportReason.Harassment
            }));

        Assert.Null(fixture.SavedReport);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task GetAdminDetail_AuditsMessageEvidenceAccess()
    {
        var fixture = new Fixture();
        var report = new UserReport(fixture.Reporter.Id, fixture.Reported.Id,
            ReportReason.Harassment, "review", fixture.Conversation.Id);
        var evidence = new Message(fixture.Conversation.Id, fixture.Reported.Id,
            MessageType.Text, "reported content");
        report.AddMessageSnapshot(new ReportedMessage(report.Id, evidence));
        fixture.ReportToView = report;

        var response = await fixture.Service.GetAdminDetailAsync(fixture.Moderator.Id, report.Id);

        Assert.Single(response.Messages);
        Assert.Contains(fixture.Audits, x => x.Action == ModerationActionType.ReportViewed);
        Assert.Contains(fixture.Audits, x => x.Action == ModerationActionType.ReportEvidenceViewed &&
            x.ReportId == report.Id && x.ActorId == fixture.Moderator.Id);
        Assert.Equal(1, fixture.SaveCount);
    }

    private sealed class Fixture
    {
        public User Reporter { get; } = new("reporter@test.invalid", "hash");
        public User Reported { get; } = new("reported@test.invalid", "hash");
        public User Moderator { get; } = new("moderator@test.invalid", "hash");
        public Conversation Conversation { get; }
        public UserReport? SavedReport { get; private set; }
        public UserReport? ReportToView { get; set; }
        public List<ModerationAudit> Audits { get; } = [];
        public int SaveCount { get; private set; }
        public ReportService Service { get; }

        public Fixture()
        {
            Conversation = new Conversation(Reporter.Id, Reported.Id);
            Moderator.ChangeRole(UserRole.Moderator);
            Service = new ReportService(
                Proxy<IReportRepository>((method, args) => method.Name switch
                {
                    nameof(IReportRepository.AddAsync) => CaptureReport(args),
                    nameof(IReportRepository.GetByIdAsync) => Task.FromResult(ReportToView),
                    nameof(IReportRepository.AddAuditAsync) => CaptureAudit(args),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IMessageRepository>((method, args) => method.Name switch
                {
                    nameof(IMessageRepository.GetByIdsAsync) => Task.FromResult(
                        (List<Message>)((IReadOnlyCollection<Guid>)args![0]!).Select(_ =>
                            new Message(Conversation.Id, Reporter.Id, MessageType.Text, "evidence")).ToList()),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IConversationRepository>((method, _) => method.Name switch
                {
                    nameof(IConversationRepository.GetByIdAsync) => Task.FromResult<Conversation?>(Conversation),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUserRepository>((method, args) => method.Name switch
                {
                    nameof(IUserRepository.GetByIdAsync) when (Guid)args![0]! == Reporter.Id =>
                        Task.FromResult<User?>(Reporter),
                    nameof(IUserRepository.GetByIdAsync) when (Guid)args![0]! == Reported.Id =>
                        Task.FromResult<User?>(Reported),
                    nameof(IUserRepository.GetByIdAsync) when (Guid)args![0]! == Moderator.Id =>
                        Task.FromResult<User?>(Moderator),
                    nameof(IUserRepository.GetByIdAsync) => Task.FromResult<User?>(null),
                    _ => throw new NotSupportedException(method.Name)
                }),
                Proxy<IUnitOfWork>((method, _) => method.Name switch
                {
                    nameof(IUnitOfWork.SaveChangesAsync) => SaveChanges(),
                    _ => throw new NotSupportedException(method.Name)
                }));
        }

        private Task CaptureReport(object?[]? args)
        {
            SavedReport = Assert.IsType<UserReport>(args![0]);
            return Task.CompletedTask;
        }

        private Task CaptureAudit(object?[]? args)
        {
            Audits.Add(Assert.IsType<ModerationAudit>(args![0]));
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
