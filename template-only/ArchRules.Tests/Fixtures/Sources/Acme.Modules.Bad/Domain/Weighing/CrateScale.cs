using Acme.SharedKernel;

namespace Acme.Modules.Bad.Domain.Weighing;

internal static class CrateScale
{
    public static Boolean IsEmpty(Weight weight) => weight.Kilograms == Decimal.Zero;
}
