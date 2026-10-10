using Acme.Modules.Bad.Features.RenameCrate;
using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.LoopBack;

internal sealed class LoopBackService(IBadModule badModule)
{
    public Task<Result<Unit>> RunAsync(RenameCrateCommand command, CancellationToken cancellationToken) =>
        badModule.ExecuteCommandAsync(command, cancellationToken);
}
