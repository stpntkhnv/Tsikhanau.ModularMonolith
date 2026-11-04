using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Infrastructure.Dispatching;
using Tsikhanau.Monolith.Infrastructure.Persistence.Commits;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.UnitTests.Dispatching;

public sealed class ModuleFacadeTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Marked_command_is_not_sent_again_for_a_conflict_its_handler_builds_with_the_unit_of_work_code()
    {
        var conflictCommandHandler = new ConflictCommandHandler();
        await using var serviceProvider = CreateProvider(conflictCommandHandler);

        var result = await serviceProvider
            .GetRequiredService<TestFacade>()
            .ExecuteCommandAsync(new ConflictCommand(), CancellationToken);

        result.Error.ShouldBe(ConflictCommandHandler.HandlerConflict);
        conflictCommandHandler.HandledCount.ShouldBe(1);
    }

    private static ServiceProvider CreateProvider(ConflictCommandHandler conflictCommandHandler) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<TestFacade>()
            .AddScoped<ModuleDispatcher<ITestModule>>()
            .AddSingleton<ICommandHandler<ConflictCommand, Unit>>(conflictCommandHandler)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    private interface ITestModule : IModuleFacade;

    private sealed record ConflictCommand : ICommand<Unit>, IRetryOnConflict;

    private sealed class TestFacade(IServiceScopeFactory serviceScopeFactory)
        : ModuleFacade<ITestModule>(serviceScopeFactory)
    {
        public Task<Result<TResult>> ExecuteCommandAsync<TResult>(
            ICommand<TResult> command,
            CancellationToken cancellationToken)
            where TResult : notnull =>
            SendAsync(command, cancellationToken);
    }

    private sealed class ConflictCommandHandler : ICommandHandler<ConflictCommand, Unit>
    {
        public static readonly Error HandlerConflict = Error.Conflict(
            UnitOfWorkErrors.ConcurrencyConflict.Code,
            "The handler found a conflict");

        public Int32 HandledCount { get; private set; }

        public Task<Result<Unit>> Handle(ConflictCommand command, CancellationToken cancellationToken)
        {
            HandledCount++;
            return Task.FromResult(Result.Failure(HandlerConflict));
        }
    }
}
