using System.Reflection;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal sealed record LoadedAssemblies(IReadOnlyList<Assembly> Assemblies, Optional<String> Failure)
{
    public static LoadedAssemblies Load(Func<IReadOnlyList<Assembly>> loadAssemblies)
    {
        try
        {
            return new LoadedAssemblies(loadAssemblies(), Optional<String>.None());
        }
        catch (Exception exception) when (IsLoadFailure(exception))
        {
            return new LoadedAssemblies([], exception.Message.TrimEnd());
        }
    }

    private static Boolean IsLoadFailure(Exception exception) =>
        exception is FileNotFoundException or FileLoadException or BadImageFormatException;
}
