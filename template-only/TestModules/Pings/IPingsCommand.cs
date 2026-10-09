using Tsikhanau.Monolith.Application.Commands;

namespace Tsikhanau.Monolith.TestModules.Pings;

internal interface IPingsCommand<TResult> : ICommand<TResult>
    where TResult : notnull;
