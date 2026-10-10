using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Tsikhanau.Monolith.Shared.Collections;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.Testing.App;

internal sealed class ModuleTestSetups
{
    private readonly IReadOnlyList<IModuleTestSetup> _setups;

    private ModuleTestSetups(IReadOnlyList<IModuleTestSetup> setups)
    {
        _setups = setups;
    }

    public static ModuleTestSetups None { get; } = new([]);

    public IEnumerable<KeyValuePair<String, String>> Settings => _setups.SelectMany(setup => setup.Settings);

    public static ModuleTestSetups Create(Assembly fixtureAssembly) => new([.. CreateSetups(fixtureAssembly)]);

    public static IEnumerable<Assembly> GetTestAssemblies(Assembly fixtureAssembly) =>
        new[] { Assembly.GetEntryAssembly(), fixtureAssembly }
            .OfType<Assembly>()
            .Distinct();

    public void ConfigureServices(IServiceCollection services) =>
        _setups.ForEach(setup => setup.ConfigureServices(services));

    private static IEnumerable<IModuleTestSetup> CreateSetups(Assembly fixtureAssembly) =>
        GetTestAssemblies(fixtureAssembly)
            .SelectMany(assembly => assembly.GetTypes())
            .Implementing<IModuleTestSetup>()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => type.CreateInstance<IModuleTestSetup>());
}
