using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Modules;

public sealed record ModuleDescriptor(
    Type Key,
    Type QueryFacadeKey,
    String Name,
    String Schema,
    Type DbContextType,
    IReadOnlyDictionary<String, Error> ConstraintErrors);
