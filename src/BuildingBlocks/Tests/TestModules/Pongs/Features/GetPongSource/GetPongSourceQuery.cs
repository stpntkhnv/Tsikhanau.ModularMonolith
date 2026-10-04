using ModularMonolith.TestModules.Pongs.Contracts;

namespace ModularMonolith.TestModules.Pongs.Features.GetPongSource;

internal sealed record GetPongSourceQuery(Guid PongId) : IPongsQuery<PongSourceDto>;
