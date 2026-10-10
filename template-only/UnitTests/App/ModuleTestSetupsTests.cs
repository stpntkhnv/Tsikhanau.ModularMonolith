using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.UnitTests.App;

public sealed class ModuleTestSetupsTests
{
    [Fact]
    public void Setups_are_looked_for_in_the_entry_assembly_and_in_the_assembly_of_the_fixture()
    {
        var fixtureAssembly = typeof(IModuleTestSetup).Assembly;

        var testAssemblies = ModuleTestSetups.GetTestAssemblies(fixtureAssembly);

        testAssemblies.ShouldBe([typeof(ModuleTestSetupsTests).Assembly, fixtureAssembly]);
    }
}
