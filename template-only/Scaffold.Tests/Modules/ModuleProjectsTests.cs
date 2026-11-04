using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Tests.Modules;

public sealed class ModuleProjectsTests
{
    [Theory]
    [InlineData(@"..\..\Notes\Acme.Shop.Modules.Notes.Contracts\Acme.Shop.Modules.Notes.Contracts.csproj", "Notes")]
    [InlineData("../../Notes/Acme.Shop.Modules.Notes.Contracts/Acme.Shop.Modules.Notes.Contracts.csproj", "Notes")]
    [InlineData(@"..\Acme.Shop.Modules.Tags.Contracts\Acme.Shop.Modules.Tags.Contracts.csproj", "Tags")]
    [InlineData(@"..\..\..\Core\Contracts\Tsikhanau.Monolith.Contracts.csproj", "")]
    [InlineData(@"..\..\Shared\Acme.Shop.Shared.Contracts\Acme.Shop.Shared.Contracts.csproj", "")]
    [InlineData(@"..\..\Notes\Contoso.Hub.Modules.Notes.Contracts\Contoso.Hub.Modules.Notes.Contracts.csproj", "")]
    [InlineData(@"..\..\Notes\Acme.Shop.Modules.Notes\Acme.Shop.Modules.Notes.csproj", "")]
    [InlineData(@"..\Acme.Shop.Modules.Contracts\Acme.Shop.Modules.Contracts.csproj", "")]
    public void Module_is_read_from_the_contracts_project_a_reference_includes(String include, String module)
    {
        var referencedModule = ModuleProjects.FindModuleOfContracts(include, "Acme.Shop");

        referencedModule.ShouldBe(module.Length == 0 ? Optional<String>.None() : module);
    }
}
