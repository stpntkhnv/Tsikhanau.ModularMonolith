using Acme.Modules.Bad.Contracts;

namespace Acme.Modules.Bad.Features.CountCrates;

internal sealed record CountCratesQuery : IBadQuery<Int32>;
