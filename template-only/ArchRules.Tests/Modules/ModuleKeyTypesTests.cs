using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.ArchRules.Support;

namespace Tsikhanau.Monolith.ArchRules.Tests.Modules;

public sealed class ModuleKeyTypesTests
{
    [Fact]
    public void Every_public_core_type_keyed_by_a_module_is_listed_once()
    {
        var listed = ModuleKeyTypes.Keys.Concat(ModuleKeyTypes.Facades);

        GetPublicTypesKeyedByAModule()
            .Except(listed)
            .ShouldBeEmpty();

        listed
            .Except(GetPublicTypesKeyedByAModule())
            .ShouldBeEmpty();

        ModuleKeyTypes.Keys
            .Intersect(ModuleKeyTypes.Facades)
            .ShouldBeEmpty();
    }

    private static IEnumerable<Type> GetPublicTypesKeyedByAModule() =>
        CoreAssemblies.BuildingBlocks
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.IsGenericTypeDefinition)
            .Where(HasModuleKey)
            .Distinct();

    private static Boolean HasModuleKey(Type type) =>
        type
            .GetGenericArguments()
            .Any(NamesModule);

    private static Boolean NamesModule(Type typeParameter) =>
        typeParameter
            .GetGenericParameterConstraints()
            .Contains(typeof(IModuleFacade));
}
