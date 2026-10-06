using System.Reflection;
using ModularMonolith.ArchTests.Support;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.Shared.Reflection;
using Tsikhanau.Railway;

namespace ModularMonolith.ArchTests.Modules;

public sealed class InternalCommandNameTests
{
    [Fact]
    public void Every_internal_command_has_a_stable_name_unique_in_its_module()
    {
        var commandTypesByModule = ArchModules.Names
            .Select(GetCommandTypes)
            .ToList();

        Assert.SkipWhen(
            commandTypesByModule.All(commandTypes => commandTypes.Count == 0),
            "No module has internal commands");

        var unnamedCommandTypeNames = commandTypesByModule
            .SelectMany(commandTypes => commandTypes)
            .Where(type => FindCommandName(type).IsNone)
            .Select(type => type.FullName);

        var sharedCommandNames = commandTypesByModule.SelectMany(GetSharedCommandNames);

        unnamedCommandTypeNames.ShouldBeEmpty();
        sharedCommandNames.ShouldBeEmpty();
    }

    private static List<Type> GetCommandTypes(String name) =>
        ArchModules
            .Get(name).Types
            .Implementing<InternalCommand>()
            .ToList();

    private static IEnumerable<String> GetSharedCommandNames(IEnumerable<Type> commandTypes) =>
        commandTypes
            .Select(FindCommandName)
            .Where(name => name.HasValue)
            .GroupBy(name => name.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

    private static Optional<String> FindCommandName(Type type) =>
        Optional<String>
            .FromNullable(type.GetCustomAttribute<InternalCommandNameAttribute>()?.Name)
            .Where(name => !String.IsNullOrWhiteSpace(name));
}
