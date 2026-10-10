using Acme.Modules.Bad.Features.SizeCrate;

namespace Acme.Modules.Bad.Settings;

internal sealed record BadSettings
{
    public required CrateSize DefaultSize { get; init; }
}
