using System.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Inputs;

public interface IAppArchRuleInputs : IArchRuleInputs
{
    static abstract Assembly HostAssembly { get; }
}
