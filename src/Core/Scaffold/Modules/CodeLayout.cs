using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal static class CodeLayout
{
    public const Int32 MaxLineLength = 120;

    public static readonly IComparer<String> NamespaceOrder =
        Comparer<String>.Create(CompareNamespaces);

    public static String FirstThatFits(params IReadOnlyList<String> candidates) =>
        candidates.FirstOrDefault(Fits) ?? candidates[^1];

    public static String Usings(params IEnumerable<String> namespaces) =>
        String.Join(
            '\n',
            namespaces
                .Order(NamespaceOrder)
                .Select(name => $"using {name};"));

    public static Optional<String> FindTooLongLine(IEnumerable<NewFile> newFiles) =>
        newFiles
            .Where(newFile => newFile.Path.EndsWith(".cs", StringComparison.Ordinal))
            .SelectMany(FindTooLongLines)
            .FirstOrDefault();

    private static IEnumerable<String> FindTooLongLines(NewFile newFile) =>
        newFile.Content
            .Split('\n')
            .Select((line, index) => (Length: line.Length, Number: index + 1))
            .Where(line => line.Length > MaxLineLength)
            .Select(line => $"{newFile.Path}:{line.Number} {line.Length} characters long");

    private static Int32 CompareNamespaces(String first, String second) =>
        IsSystem(first) == IsSystem(second)
            ? String.CompareOrdinal(first, second)
            : IsSystem(first) ? -1 : 1;

    private static Boolean IsSystem(String name) =>
        name == "System" || name.StartsWith("System.", StringComparison.Ordinal);

    private static Boolean Fits(String code) =>
        code
            .Split('\n')
            .All(line => line.Length <= MaxLineLength);
}
