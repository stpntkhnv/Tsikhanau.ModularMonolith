using ModularMonolith.BuildingBlocks.Application.Modules;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;

public sealed class InternalCommandRegistry<TModule>(IEnumerable<Type> commandTypes)
    where TModule : IModuleFacade
{
    private readonly Dictionary<String, Type> _commandTypes = commandTypes.ToDictionary(GetName);

    public static String GetName(Type commandType) => commandType.FullName!;

    public Optional<Type> FindType(String name) => _commandTypes.GetValueOrDefault(name);
}
