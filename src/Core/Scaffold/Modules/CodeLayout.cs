using Tsikhanau.Monolith.Scaffold.Transactions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Modules;

internal static class CodeLayout
{
    public const Int32 MaxLineLength = 120;

    private const String LineTooLong = "more than the 120 the build allows; choose a shorter";

    public static readonly StringComparer NameOrder = StringComparer.OrdinalIgnoreCase;

    public static readonly IComparer<String> NamespaceOrder =
        Comparer<String>.Create(CompareNamespaces);

    public static readonly IComparer<String> PathOrder =
        Comparer<String>.Create(ComparePaths);

    private static readonly Char[] PathSeparators = ['/', '\\'];

    public static String FirstThatFits(params IReadOnlyList<String> candidates) =>
        candidates.FirstOrDefault(Fits) ?? candidates[^1];

    public static String Usings(params IEnumerable<String> namespaces) =>
        String.Join(
            '\n',
            namespaces
                .Distinct(StringComparer.Ordinal)
                .Order(NamespaceOrder)
                .Select(name => $"using {name};"));

    public static Result<IReadOnlyList<NewFile>> EnsureLinesFit(
        IReadOnlyList<NewFile> newFiles,
        String chosenName,
        String shorterChoice) =>
        FindTooLongLine(newFiles)
            .Match(
                tooLongLine => Error.Failure(
                    "scaffold.name",
                    $"{chosenName} makes {tooLongLine}, {LineTooLong} {shorterChoice}"),
                () => Result.Success(newFiles));

    public static Optional<String> FindTooLongLine(IEnumerable<NewFile> newFiles) =>
        newFiles
            .Where(newFile => newFile.Path.EndsWith(".cs", StringComparison.Ordinal))
            .SelectMany(GetTooLongLines)
            .FirstOrDefault();

    private static IEnumerable<String> GetTooLongLines(NewFile newFile) =>
        newFile.Content
            .Split('\n')
            .Select((line, index) => (Length: line.Length, Number: index + 1))
            .Where(line => line.Length > MaxLineLength)
            .Select(line => $"{newFile.Path}:{line.Number} {line.Length} characters long");

    private static Int32 CompareNamespaces(String first, String second) =>
        IsSystem(first) == IsSystem(second)
            ? NameOrder.Compare(first, second)
            : IsSystem(first) ? -1 : 1;

    private static Int32 ComparePaths(String first, String second)
    {
        var firstSegments = first.Split(PathSeparators);
        var secondSegments = second.Split(PathSeparators);

        return firstSegments
            .Zip(secondSegments, NameOrder.Compare)
            .Where(order => order != 0)
            .DefaultIfEmpty(firstSegments.Length.CompareTo(secondSegments.Length))
            .First();
    }

    private static Boolean IsSystem(String name) =>
        name == "System" || name.StartsWith("System.", StringComparison.Ordinal);

    private static Boolean Fits(String code) =>
        code
            .Split('\n')
            .All(line => line.Length <= MaxLineLength);
}
