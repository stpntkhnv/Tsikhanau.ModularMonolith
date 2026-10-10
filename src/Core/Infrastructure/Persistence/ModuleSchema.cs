using System.Reflection;

namespace Tsikhanau.Monolith.Infrastructure.Persistence;

internal static class ModuleSchema
{
    private const String ConstantName = "SchemaName";

    private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private const String MissingConstantProblem = $"must name its schema in a const String {ConstantName}";

    public static String Get(Type dbContextType) =>
        dbContextType.GetField(ConstantName, StaticFields) is { IsLiteral: true } schemaField
            && schemaField.GetRawConstantValue() is String schema
            ? schema
            : throw new InvalidOperationException(
                $"Module db context {dbContextType.FullName} {MissingConstantProblem}");
}
