using System.Globalization;
using EFCore.NamingConventions.Internal;

namespace Tsikhanau.Monolith.Infrastructure.Persistence.Naming;

internal static class ColumnNames
{
    private static readonly SnakeCaseNameRewriter SnakeCase = new(CultureInfo.InvariantCulture);

    public static String Of(String propertyName) => SnakeCase.RewriteName(propertyName);
}
