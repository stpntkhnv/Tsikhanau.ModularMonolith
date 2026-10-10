using Acme.Modules.Bad.Infrastructure.Clocks;

namespace Acme.Modules.Bad.Features.NameCrate;

internal sealed class CrateNamer
{
    public String Name(BadClock clock) => clock.Tag;
}
