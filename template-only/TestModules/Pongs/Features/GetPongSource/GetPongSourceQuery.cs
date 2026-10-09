using Tsikhanau.Monolith.TestModules.Pongs.Contracts;

namespace Tsikhanau.Monolith.TestModules.Pongs.Features.GetPongSource;

internal sealed record GetPongSourceQuery(Guid PongId) : IPongsQuery<PongSourceDto>;
