using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Projects.Hosts;
using Tsikhanau.Monolith.Testing.Modules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Projects;

public abstract class BootstrapperTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IAppArchRuleInputs
{
    [Fact]
    public void Host_adds_every_module_folder() =>
        Check(
            HostRules.HostAddsEveryModuleFolder(TInputs.HostAssembly, Modules),
            ProductionModules.NoModuleReason,
            "Add each module of src/Modules to the host once with AddModule, and nothing else");
}
