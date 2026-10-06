using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.Shared.Reflection;

namespace ModularMonolith.BuildingBlocks.Infrastructure.InternalCommands;

internal static class InternalCommandNameValidator
{
    public static IReadOnlyList<String> CollectProblems(IReadOnlyCollection<Type> types)
    {
        var commandTypes = types
            .Implementing<InternalCommand>()
            .ToList();

        return
        [
            .. CollectUnnamedProblems(commandTypes),
            .. CollectBlankNameProblems(commandTypes),
            .. CollectSharedNameProblems(commandTypes),
        ];
    }

    private static IEnumerable<String> CollectUnnamedProblems(IEnumerable<Type> commandTypes) =>
        commandTypes
            .Where(type => !type.HasInternalCommandNameAttribute)
            .Select(type => $"{type.FullName} has no InternalCommandName attribute");

    private static IEnumerable<String> CollectBlankNameProblems(IEnumerable<Type> commandTypes) =>
        commandTypes
            .Where(type => type.HasInternalCommandNameAttribute && type.FindInternalCommandName().IsNone)
            .Select(type => $"{type.FullName} has a blank InternalCommandName");

    private static IEnumerable<String> CollectSharedNameProblems(IEnumerable<Type> commandTypes) =>
        commandTypes
            .Where(type => type.FindInternalCommandName().HasValue)
            .GroupBy(type => type.FindInternalCommandName().Value)
            .Where(group => group.Count() > 1)
            .Select(DescribeSharedName);

    private static String DescribeSharedName(IGrouping<String, Type> commandTypesWithOneName) =>
        $"Internal command name '{commandTypesWithOneName.Key}' is used by "
        + String.Join(" and ", commandTypesWithOneName.Select(type => type.FullName));
}
