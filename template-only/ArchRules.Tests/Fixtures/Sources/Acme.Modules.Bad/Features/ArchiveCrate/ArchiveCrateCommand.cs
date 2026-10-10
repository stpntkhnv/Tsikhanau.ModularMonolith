using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.ArchiveCrate;

internal sealed record ArchiveCrateCommand(Guid CrateId) : IBadCommand<Unit>;
