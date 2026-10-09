using System.Reflection;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal sealed record LoadedAssemblies(
    IReadOnlyList<Assembly> Assemblies,
    IReadOnlyList<Type> Types,
    Optional<String> Failure)
{
    public static LoadedAssemblies Load(Func<IReadOnlyList<Assembly>> loadAssemblies)
    {
        try
        {
            var assemblies = loadAssemblies();
            return new LoadedAssemblies(assemblies, ReadTypes(assemblies), Optional<String>.None());
        }
        catch (ReflectionTypeLoadException exception)
        {
            return Failed(DescribeTypeLoadFailures(exception));
        }
        catch (Exception exception) when (IsLoadFailure(exception))
        {
            return Failed(exception.Message.TrimEnd());
        }
    }

    private static IReadOnlyList<Type> ReadTypes(IReadOnlyList<Assembly> assemblies) =>
    [
        .. assemblies.SelectMany(assembly => assembly.GetTypes()),
    ];

    private static LoadedAssemblies Failed(String failure) => new([], [], failure);

    private static String DescribeTypeLoadFailures(ReflectionTypeLoadException exception) =>
        String.Join(
            "; ",
            exception.LoaderExceptions
                .OfType<Exception>()
                .Select(loaderException => loaderException.Message.TrimEnd())
                .DefaultIfEmpty(exception.Message.TrimEnd())
                .Distinct());

    private static Boolean IsLoadFailure(Exception exception) =>
        exception is FileNotFoundException or FileLoadException or BadImageFormatException;
}
