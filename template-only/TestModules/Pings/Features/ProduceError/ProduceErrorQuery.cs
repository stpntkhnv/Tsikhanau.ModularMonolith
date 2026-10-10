using Tsikhanau.Monolith.TestModules.Pings.Contracts;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ProduceError;

internal sealed record ProduceErrorQuery(ProducedErrorKind Kind) : IPingsQuery<Unit>;
