using Tsikhanau.Monolith.Contracts.Queries;

namespace Tsikhanau.Monolith.TestModules.Unhandled;

internal interface IUnhandledQuery<TResult> : IQuery<TResult>
    where TResult : notnull;
