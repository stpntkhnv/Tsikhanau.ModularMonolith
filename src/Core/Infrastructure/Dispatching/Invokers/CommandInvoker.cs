using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Dispatching.Invokers;

internal abstract class CommandInvoker<TResult>
    where TResult : notnull
{
    private static readonly ConcurrentDictionary<Type, CommandInvoker<TResult>> Cache = new();

    public static CommandInvoker<TResult> Get(Type commandType) =>
        Cache.GetOrAdd(commandType, static type =>
            typeof(CommandInvoker<,>)
                .MakeGenericType(type, typeof(TResult))
                .CreateInstance<CommandInvoker<TResult>>());

    public abstract Task<Result<TResult>> InvokeAsync(
        ICommand<TResult> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class CommandInvoker<TCommand, TResult> : CommandInvoker<TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{
    public override Task<Result<TResult>> InvokeAsync(
        ICommand<TResult> command,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        serviceProvider
            .GetRequiredService<ICommandHandler<TCommand, TResult>>()
            .Handle((TCommand)command, cancellationToken);
}
