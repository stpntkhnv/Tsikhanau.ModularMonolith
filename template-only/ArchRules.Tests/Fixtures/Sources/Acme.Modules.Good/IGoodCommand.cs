using Tsikhanau.Monolith.Application.Commands;

namespace Acme.Modules.Good;

internal interface IGoodCommand<TResult> : ICommand<TResult>
    where TResult : notnull;
