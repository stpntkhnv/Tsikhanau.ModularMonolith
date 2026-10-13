using Acme.SharedKernel;

namespace Acme.Modules.Bad.Features.KeyCrate;

internal sealed class CrateKeyService(Keyed<IBadModule> keyedModule)
{
    public String Describe() => keyedModule.Name;
}
