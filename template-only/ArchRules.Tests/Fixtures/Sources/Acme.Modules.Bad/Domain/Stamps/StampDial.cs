using Acme.SharedKernel;

namespace Acme.Modules.Bad.Domain.Stamps;

internal static class StampDial
{
    public static DateTimeOffset Read(SystemClock systemClock) => systemClock.Today();
}
