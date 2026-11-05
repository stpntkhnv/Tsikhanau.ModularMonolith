using System.Reflection;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Infrastructure.InternalCommands;

internal static class InternalCommandTypeExtensions
{
    public static Boolean HasInternalCommandNameAttribute(this Type commandType) =>
        commandType.IsDefined(typeof(InternalCommandNameAttribute), inherit: false);

    public static Optional<String> FindInternalCommandName(this Type commandType) =>
        Optional<String>
            .FromNullable(commandType.GetCustomAttribute<InternalCommandNameAttribute>()?.Name)
            .Where(name => !String.IsNullOrWhiteSpace(name));
}
