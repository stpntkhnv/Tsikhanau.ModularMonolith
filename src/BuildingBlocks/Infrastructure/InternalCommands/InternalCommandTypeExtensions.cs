using System.Reflection;
using ModularMonolith.BuildingBlocks.Application.Commands;
using Tsikhanau.Railway;

namespace ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;

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
