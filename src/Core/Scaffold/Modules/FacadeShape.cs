namespace Tsikhanau.Monolith.Scaffold.Modules;

internal sealed record FacadeShape(Boolean HasCommands)
{
    public static readonly FacadeShape Fresh = new(HasCommands: false);

    public FacadeShape WithCommands() => this with { HasCommands = true };
}
