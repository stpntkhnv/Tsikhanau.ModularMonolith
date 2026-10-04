using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Application.Modules;
using ModularMonolith.BuildingBlocks.Application.Queries;
using ModularMonolith.BuildingBlocks.Contracts.Queries;
using ModularMonolith.BuildingBlocks.Infrastructure.Dispatching;
using ModularMonolith.BuildingBlocks.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.UnitTests.Dispatching;

public sealed class ModuleDispatcherTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Command_is_handled_and_committed()
    {
        var fakeUnitOfWork = new FakeUnitOfWork();

        var result = await SendAsync(new DoubleCommand(21), fakeUnitOfWork);

        result.Value.ShouldBe(42);
        fakeUnitOfWork.Commits.ShouldBe(1);
    }

    [Fact]
    public async Task Failed_command_is_not_committed()
    {
        var fakeUnitOfWork = new FakeUnitOfWork();

        var result = await SendAsync(new RejectCommand(), fakeUnitOfWork);

        result.Error.ShouldBe(RejectCommandHandler.Rejected);
        fakeUnitOfWork.Commits.ShouldBe(0);
    }

    [Fact]
    public async Task Commit_failure_becomes_the_command_failure()
    {
        var commitError = Error.Conflict("Test.Commit", "Commit failed");
        var fakeUnitOfWork = new FakeUnitOfWork { NextCommit = Result.Failure(commitError) };

        var result = await SendAsync(new DoubleCommand(1), fakeUnitOfWork);

        result.Error.ShouldBe(commitError);
    }

    [Fact]
    public async Task Throwing_handler_surfaces_the_exception_and_skips_the_commit()
    {
        var fakeUnitOfWork = new FakeUnitOfWork();

        await Should.ThrowAsync<InvalidOperationException>(() => SendAsync(new ThrowCommand(), fakeUnitOfWork));

        fakeUnitOfWork.Commits.ShouldBe(0);
    }

    [Fact]
    public async Task Query_is_handled_without_a_commit()
    {
        var fakeUnitOfWork = new FakeUnitOfWork();

        var result = await QueryAsync(new EchoQuery("hi"), fakeUnitOfWork);

        result.Value.ShouldBe("hi");
        fakeUnitOfWork.Commits.ShouldBe(0);
    }

    private static async Task<Result<TResult>> SendAsync<TResult>(
        ICommand<TResult> command,
        FakeUnitOfWork fakeUnitOfWork)
        where TResult : notnull
    {
        await using var serviceProvider = CreateProvider(fakeUnitOfWork);
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ModuleDispatcher<ITestModule>>()
            .SendAsync(command, CancellationToken);
    }

    private static async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, FakeUnitOfWork fakeUnitOfWork)
        where TResult : notnull
    {
        await using var serviceProvider = CreateProvider(fakeUnitOfWork);
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ModuleDispatcher<ITestModule>>()
            .QueryAsync(query, CancellationToken);
    }

    private static ServiceProvider CreateProvider(FakeUnitOfWork fakeUnitOfWork) =>
        new ServiceCollection()
            .AddLogging()
            .AddScoped<ModuleDispatcher<ITestModule>>()
            .AddSingleton<IUnitOfWork<ITestModule>>(fakeUnitOfWork)
            .AddScoped<ICommandHandler<DoubleCommand, Int32>, DoubleCommandHandler>()
            .AddScoped<ICommandHandler<RejectCommand, Unit>, RejectCommandHandler>()
            .AddScoped<ICommandHandler<ThrowCommand, Unit>, ThrowCommandHandler>()
            .AddScoped<IQueryHandler<EchoQuery, String>, EchoQueryHandler>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private interface ITestModule : IModuleFacade;

    private sealed record DoubleCommand(Int32 Value) : ICommand<Int32>;

    private sealed record RejectCommand : ICommand<Unit>;

    private sealed record ThrowCommand : ICommand<Unit>;

    private sealed record EchoQuery(String Text) : IQuery<String>;

    private sealed class DoubleCommandHandler : ICommandHandler<DoubleCommand, Int32>
    {
        public Task<Result<Int32>> Handle(DoubleCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(command.Value * 2));
    }

    private sealed class RejectCommandHandler : ICommandHandler<RejectCommand, Unit>
    {
        public static readonly Error Rejected = Error.Conflict("Test.Rejected", "Rejected on purpose");

        public Task<Result<Unit>> Handle(RejectCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure(Rejected));
    }

    private sealed class ThrowCommandHandler : ICommandHandler<ThrowCommand, Unit>
    {
        public Task<Result<Unit>> Handle(ThrowCommand command, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class EchoQueryHandler : IQueryHandler<EchoQuery, String>
    {
        public Task<Result<String>> Handle(EchoQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(query.Text));
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
}
