using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.Contracts.Secrets;
using Tsikhanau.Monolith.Infrastructure.Queues;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.InternalCommands;

public sealed class InternalCommandRegistry<TModule>
    where TModule : IModuleFacade
{
    private readonly Dictionary<Type, String> _names;

    private readonly Dictionary<String, Type> _commandTypes;

    public InternalCommandRegistry(IEnumerable<Type> commandTypes)
    {
        _names = commandTypes.ToDictionary(type => type, type => type.FindInternalCommandName().Value);
        _commandTypes = _names.ToDictionary(entry => entry.Value, entry => entry.Key);
    }

    private static String ModuleName => typeof(TModule).Name;

    public String GetName(Type commandType) =>
        _names.TryGetValue(commandType, out var name)
            ? name
            : throw new InvalidOperationException($"{commandType.FullName} is not an internal command of {ModuleName}");

    public Optional<Type> FindType(String name) => _commandTypes.GetValueOrDefault(name);

    public void EraseSecretsOf(QueueMessage message)
    {
        if (ContainsSecrets(message.Type))
        {
            message.ErasePayload();
        }
    }

    private Boolean ContainsSecrets(String name) =>
        FindType(name)
            .Map(type => type.IsDefined(typeof(ContainsSecretsAttribute), inherit: false))
            .GetValueOrDefault(false);
}
