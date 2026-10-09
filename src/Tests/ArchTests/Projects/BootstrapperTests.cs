using Mono.Cecil;
using Tsikhanau.Monolith.Api.Modules;
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Projects;

public sealed class BootstrapperTests
{
    [Fact]
    public void Host_adds_every_module_folder()
    {
        var addedModuleNames = ReadModulesAddedByTheHost();
        Assert.SkipWhen(
            ProductionModules.Names.Length == 0 && addedModuleNames.Count == 0,
            "There are no production modules");

        var expectedModuleNames = ProductionModules.Names.Select(BuildModuleClassName);

        addedModuleNames.ShouldBe(expectedModuleNames, ignoreOrder: true);
    }

    private static String BuildModuleClassName(String name) =>
        $"ModularMonolith.Modules.{name}.{name}Module";

    private static List<String> ReadModulesAddedByTheHost()
    {
        using var assembly = AssemblyDefinition.ReadAssembly(typeof(Program).Assembly.Location);
        return assembly.MainModule
            .GetTypes()
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Select(instruction => instruction.Operand)
            .OfType<GenericInstanceMethod>()
            .Where(calledMethod => calledMethod.Name == nameof(ModuleHostExtensions.AddModule))
            .Select(calledMethod => calledMethod.GenericArguments.Single().FullName)
            .ToList();
    }
}
