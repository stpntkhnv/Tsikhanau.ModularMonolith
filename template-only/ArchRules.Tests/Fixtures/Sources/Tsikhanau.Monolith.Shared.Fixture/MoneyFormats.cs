using System.Globalization;
using Acme.SharedKernel;

namespace Tsikhanau.Monolith.Shared.Fixture;

public static class MoneyFormats
{
    public static String Format(Money money) => money.Value.ToString("0.00", CultureInfo.InvariantCulture);
}
