using Tsikhanau.Monolith.Infrastructure.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

internal sealed record MessageAddress(ModuleDescriptor Module, String Queue, Guid MessageId)
{
    public String Table => $"\"{Module.Schema}\".{Queue}";

    public override String ToString() => $"{Module.Name} {Queue} {MessageId}";
}
