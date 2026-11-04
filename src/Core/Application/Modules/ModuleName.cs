using System.Text.RegularExpressions;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Application.Modules;

public static partial class ModuleName
{
    private const String ModuleGroup = "Module";

    private const String FacadePrefix = "I";

    private const String ModuleClassProblem = "must be named <Module>Module, like NotesModule";

    private const String FacadeProblem = "must be named I<Module>Module, like INotesModule";

    public static String FromModuleClass(Type moduleType) =>
        FindName(moduleType.Name)
            .GetValueOrDefault(() => ThrowMisnamed($"Module class {moduleType.FullName} {ModuleClassProblem}"));

    public static String FromFacade(Type facadeType) =>
        FindFacadeName(facadeType.Name)
            .GetValueOrDefault(() => ThrowMisnamed($"Module facade {facadeType.FullName} {FacadeProblem}"));

    private static Optional<String> FindFacadeName(String facadeName) =>
        facadeName.StartsWith(FacadePrefix, StringComparison.Ordinal)
            ? FindName(facadeName[FacadePrefix.Length..])
            : Optional<String>.None();

    private static Optional<String> FindName(String className)
    {
        var moduleClassNamePattern = ModuleClassNamePattern();
        return moduleClassNamePattern.Match(className) is { Success: true } moduleClassName
            ? moduleClassName.Groups[ModuleGroup].Value
            : Optional<String>.None();
    }

    private static String ThrowMisnamed(String message) => throw new InvalidOperationException(message);

    [GeneratedRegex($@"\A(?<{ModuleGroup}>\p{{Lu}}\w*)Module\z")]
    private static partial Regex ModuleClassNamePattern();
}
