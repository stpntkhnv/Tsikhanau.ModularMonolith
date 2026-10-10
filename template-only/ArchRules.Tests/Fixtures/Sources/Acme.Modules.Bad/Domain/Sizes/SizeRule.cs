using Acme.Modules.Bad.Features.SizeCrate;

namespace Acme.Modules.Bad.Domain.Sizes;

internal static class SizeRule
{
    public static Boolean IsLarge(CrateSize size) => size == CrateSize.Large;
}
