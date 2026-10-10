using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Events;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Monolith.Testing.Queues;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Queues;

public sealed class UnitOfWorkDriverTests
{
    private static readonly DateTimeOffset OccurredOn = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Dispatch_returns_the_error_of_a_failed_handler_and_commits_nothing()
    {
        var fakeUnitOfWork = new FakeUnitOfWork();
        await using var serviceProvider = CreateProvider(fakeUnitOfWork, new FakeInternalCommandScheduler());

        var dispatched = await UnitOfWorkDriver.DispatchAsync<ITestModule>(
            serviceProvider,
            new RejectedDomainEvent(OccurredOn));

        dispatched.ShouldBe(Result.Failure(RejectingDomainEventHandler.Rejected));
        fakeUnitOfWork.Commits.ShouldBe(0);
    }

    [Fact]
    public async Task Dispatch_refuses_an_event_the_module_does_not_handle_and_commits_nothing()
    {
        var fakeUnitOfWork = new FakeUnitOfWork();
        await using var serviceProvider = CreateProvider(fakeUnitOfWork, new FakeInternalCommandScheduler());

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => UnitOfWorkDriver.DispatchAsync<ITestModule>(serviceProvider, new UnhandledDomainEvent(OccurredOn)));

        exception.Message.ShouldBe(
            "No handler in ITestModule handles UnhandledDomainEvent "
            + $"({typeof(UnhandledDomainEvent).FullName}); "
            + "a module handles a domain event with an IDomainEventHandler<UnhandledDomainEvent> in its own namespace");

        fakeUnitOfWork.Commits.ShouldBe(0);
    }

    [Fact]
    public async Task Enqueue_throws_with_the_error_of_a_failed_commit()
    {
        var commitError = Error.Conflict("Test.Commit", "Commit failed");
        var fakeUnitOfWork = new FakeUnitOfWork { NextCommit = Result.Failure(commitError) };
        var fakeInternalCommandScheduler = new FakeInternalCommandScheduler();
        var internalCommand = new SampleInternalCommand(Guid.CreateVersion7(OccurredOn));
        await using var serviceProvider = CreateProvider(fakeUnitOfWork, fakeInternalCommandScheduler);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => UnitOfWorkDriver.EnqueueAsync<ITestModule>(serviceProvider, internalCommand));

        exception.Message.ShouldBe("Enqueuing SampleInternalCommand failed: [Test.Commit] Commit failed");
        fakeInternalCommandScheduler.EnqueuedCommands.ShouldBe([internalCommand]);
        fakeUnitOfWork.Commits.ShouldBe(1);
    }

    private static ServiceProvider CreateProvider(
        FakeUnitOfWork fakeUnitOfWork,
        FakeInternalCommandScheduler fakeInternalCommandScheduler) =>
        new ServiceCollection()
            .AddSingleton<IUnitOfWork<ITestModule>>(fakeUnitOfWork)
            .AddSingleton<IInternalCommandScheduler<ITestModule>>(fakeInternalCommandScheduler)
            .AddScoped<IDomainEventHandler<RejectedDomainEvent>, RejectingDomainEventHandler>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private interface ITestModule : IModuleFacade;

    private sealed record RejectedDomainEvent(DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

    private sealed record UnhandledDomainEvent(DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

    private sealed record SampleInternalCommand(Guid Id) : InternalCommand(Id);

    private sealed class RejectingDomainEventHandler : IDomainEventHandler<RejectedDomainEvent>
    {
        public static readonly Error Rejected = Error.Failure("Test.Rejected", "Rejected on purpose");

        public Task<Result<Unit>> Handle(RejectedDomainEvent domainEvent, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure(Rejected));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork<ITestModule>
    {
        public Int32 Commits { get; private set; }

        public Boolean HasChanges => true;

        public Result<Unit> NextCommit { get; init; } = Result.Success();

        public Task<Result<Unit>> CommitAsync(CancellationToken cancellationToken)
        {
            Commits++;
            return Task.FromResult(NextCommit);
        }
    }

    private sealed class FakeInternalCommandScheduler : IInternalCommandScheduler<ITestModule>
    {
        private readonly List<InternalCommand> _enqueuedCommands = [];

        public IReadOnlyList<InternalCommand> EnqueuedCommands => _enqueuedCommands;

        public void Enqueue(InternalCommand command) => _enqueuedCommands.Add(command);
    }
}
