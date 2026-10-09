using System.Reflection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.InternalCommands;

internal static class InternalCommandTypeExtensions
{
    extension(Type commandType)
    {
        public Boolean HasInternalCommandNameAttribute =>
            commandType.IsDefined(typeof(InternalCommandNameAttribute), inherit: false);

        public Optional<String> FindInternalCommandName() =>
            Optional<String>
                .FromNullable(commandType.GetCustomAttribute<InternalCommandNameAttribute>()?.Name)
                .Where(name => !String.IsNullOrWhiteSpace(name));
    }
}
