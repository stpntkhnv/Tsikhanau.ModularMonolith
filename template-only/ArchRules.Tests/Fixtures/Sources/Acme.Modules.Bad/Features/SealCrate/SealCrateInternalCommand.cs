using Tsikhanau.Monolith.Application.Commands;

namespace Acme.Modules.Bad.Features.SealCrate;

internal sealed record SealCrateInternalCommand(Guid Id) : InternalCommand(Id);
