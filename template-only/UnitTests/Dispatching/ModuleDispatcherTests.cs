using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.Infrastructure.Persistence;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Dispatching;

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

    [Theory]
    [InlineData(ErrorKind.Unexpected, LogLevel.Warning)]
    [InlineData(ErrorKind.Failure, LogLevel.Debug)]
    [InlineData(ErrorKind.Validation, LogLevel.Debug)]
    [InlineData(ErrorKind.NotFound, LogLevel.Debug)]
    [InlineData(ErrorKind.Conflict, LogLevel.Debug)]
    [InlineData(ErrorKind.Unauthorized, LogLevel.Debug)]
    [InlineData(ErrorKind.Forbidden, LogLevel.Debug)]
    public async Task Failed_command_is_logged_at_warning_only_when_unexpected(
        ErrorKind errorKind,
        LogLevel expectedLogLevel)
    {
        var capturingLogger = new CapturingLogger();
        var failCommand = new FailCommand(new Error(errorKind, "Test.Failed", "Failed on purpose"));

        await SendAsync(failCommand, new FakeUnitOfWork(), capturingLogger);

        capturingLogger.LogLevels.ShouldBe([expectedLogLevel]);
    }

    [Fact]
    public async Task Query_is_handled_without_a_commit()
    {
        var fakeUnitOfWork = new FakeUnitOfWork();

        var result = await QueryAsync(new EchoQuery("hi"), fakeUnitOfWork);

        result.Value.ShouldBe("hi");
        fakeUnitOfWork.Commits.ShouldBe(0);
    }

    private static Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, FakeUnitOfWork fakeUnitOfWork)
        where TResult : notnull =>
        SendAsync(command, fakeUnitOfWork, new CapturingLogger());

    private static async Task<Result<TResult>> SendAsync<TResult>(
        ICommand<TResult> command,
        FakeUnitOfWork fakeUnitOfWork,
        CapturingLogger capturingLogger)
        where TResult : notnull
    {
        await using var serviceProvider = CreateProvider(fakeUnitOfWork, capturingLogger);
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ModuleDispatcher<ITestModule>>()
            .SendAsync(command, CancellationToken);
    }

    private static async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, FakeUnitOfWork fakeUnitOfWork)
        where TResult : notnull
    {
        await using var serviceProvider = CreateProvider(fakeUnitOfWork, new CapturingLogger());
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ModuleDispatcher<ITestModule>>()
            .QueryAsync(query, CancellationToken);
    }

    private static ServiceProvider CreateProvider(FakeUnitOfWork fakeUnitOfWork, CapturingLogger capturingLogger) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<ILogger<ModuleDispatcher<ITestModule>>>(capturingLogger)
            .AddScoped<ModuleDispatcher<ITestModule>>()
            .AddSingleton<IUnitOfWork<ITestModule>>(fakeUnitOfWork)
            .AddScoped<ICommandHandler<DoubleCommand, Int32>, DoubleCommandHandler>()
            .AddScoped<ICommandHandler<RejectCommand, Unit>, RejectCommandHandler>()
            .AddScoped<ICommandHandler<FailCommand, Unit>, FailCommandHandler>()
            .AddScoped<ICommandHandler<ThrowCommand, Unit>, ThrowCommandHandler>()
            .AddScoped<IQueryHandler<EchoQuery, String>, EchoQueryHandler>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private interface ITestModule : IModuleFacade;

    private sealed record DoubleCommand(Int32 Value) : ICommand<Int32>;

    private sealed record RejectCommand : ICommand<Unit>;

    private sealed record FailCommand(Error Error) : ICommand<Unit>;

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

    private sealed class FailCommandHandler : ICommandHandler<FailCommand, Unit>
    {
        public Task<Result<Unit>> Handle(FailCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Failure(command.Error));
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

    private sealed class CapturingLogger : ILogger<ModuleDispatcher<ITestModule>>
    {
        private readonly List<LogLevel> _logLevels = [];

        public IReadOnlyList<LogLevel> LogLevels => _logLevels;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public Boolean IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, String> formatter) =>
            _logLevels.Add(logLevel);
    }
}
