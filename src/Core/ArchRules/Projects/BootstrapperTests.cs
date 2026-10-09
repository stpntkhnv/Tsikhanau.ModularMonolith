using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public abstract class BootstrapperTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IAppArchRuleInputs
{
    [Fact]
    public void Host_adds_every_module_folder() =>
        Check(HostRules.HostAddsEveryModuleFolder(TInputs.HostAssembly, Modules), "There are no production modules");
}
