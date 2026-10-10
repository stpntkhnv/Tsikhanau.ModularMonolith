using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.RenameCrate;

internal sealed record RenameCrateCommand : IBadCommand<Unit>
{
    public String Name { get; set; } = "";
}
