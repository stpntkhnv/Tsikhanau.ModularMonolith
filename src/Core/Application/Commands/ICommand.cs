namespace Tsikhanau.Monolith.Application.Commands;

public interface ICommand<TResult>
    where TResult : notnull;
