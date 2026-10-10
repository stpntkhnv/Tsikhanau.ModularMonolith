using Tsikhanau.Monolith.Application.Commands;

namespace Acme.Modules.Bad;

internal interface IBadCommand<TResult> : ICommand<TResult>
    where TResult : notnull;
