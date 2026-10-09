using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Monolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.Dispatching.Invokers;

internal abstract class QueryInvoker<TResult>
    where TResult : notnull
{
    private static readonly ConcurrentDictionary<Type, QueryInvoker<TResult>> Cache = new();

    public static QueryInvoker<TResult> Get(Type queryType) =>
        Cache.GetOrAdd(queryType, static type =>
            typeof(QueryInvoker<,>)
                .MakeGenericType(type, typeof(TResult))
                .CreateInstance<QueryInvoker<TResult>>());

    public abstract Task<Result<TResult>> InvokeAsync(
        IQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal sealed class QueryInvoker<TQuery, TResult> : QueryInvoker<TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    public override Task<Result<TResult>> InvokeAsync(
        IQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        serviceProvider
            .GetRequiredService<IQueryHandler<TQuery, TResult>>()
            .Handle((TQuery)query, cancellationToken);
}
