using System.Text;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

internal static class ConstraintNameValidator
{
    private const Int32 MaxNameBytes = 63;

    public static IReadOnlyList<String> CollectProblems(IEnumerable<String> constraintNames) =>
        constraintNames
            .Select(name => (Name: name, ByteCount: Encoding.UTF8.GetByteCount(name)))
            .Where(constraint => constraint.ByteCount > MaxNameBytes)
            .Select(DescribeCutName)
            .ToList();

    private static String DescribeCutName((String Name, Int32 ByteCount) constraint) =>
        $"'{constraint.Name}' is {constraint.ByteCount} bytes long, PostgreSQL cuts names to {MaxNameBytes} bytes";
}
