using Tsikhanau.Monolith.Application.Commands;

namespace Tsikhanau.Monolith.TestModules.Pongs;

internal interface IPongsCommand<TResult> : ICommand<TResult>
    where TResult : notnull;
