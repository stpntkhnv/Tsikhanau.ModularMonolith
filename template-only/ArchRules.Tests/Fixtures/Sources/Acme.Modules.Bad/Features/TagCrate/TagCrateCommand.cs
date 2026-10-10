using Tsikhanau.Railway;

namespace Acme.Modules.Bad.Features.TagCrate;

internal sealed record TagCrateCommand(Guid CrateId, String? Tag) : IBadCommand<Unit>;
