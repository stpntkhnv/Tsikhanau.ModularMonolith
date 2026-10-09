using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Application.Commands;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{
    Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken);
}
