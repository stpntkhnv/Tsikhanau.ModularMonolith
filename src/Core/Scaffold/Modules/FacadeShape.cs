namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed record FacadeShape(Boolean HasCommands, Boolean HasQueries)
{
    public static readonly FacadeShape Fresh = new(HasCommands: false, HasQueries: false);

    public FacadeShape WithCommands() => this with { HasCommands = true };

    public FacadeShape WithQueries() => this with { HasQueries = true };
}
