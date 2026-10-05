using System.Reflection;
using ModularMonolith.ArchTests.Support;
using ModularMonolith.BuildingBlocks.Application.Commands;
using ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;
using ModularMonolith.BuildingBlocks.Contracts.Queries;
using ModularMonolith.BuildingBlocks.Domain.Aggregates;
using ModularMonolith.BuildingBlocks.Domain.Events;
using ModularMonolith.BuildingBlocks.Domain.Ids;
using ModularMonolith.BuildingBlocks.Domain.ValueObjects;
using ModularMonolith.Shared.Reflection;
using Mono.Cecil;

namespace ModularMonolith.ArchTests.Modules;

public sealed class DomainRulesTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly HashSet<String> ForbiddenCalls =
    [
        "System.DateTime::get_UtcNow/0",
        "System.DateTime::get_Now/0",
        "System.DateTimeOffset::get_UtcNow/0",
        "System.DateTimeOffset::get_Now/0",
        "System.Guid::NewGuid/0",
        "System.Guid::CreateVersion7/0",
    ];

    [Theory]
    [EveryModule]
    public void Domain_types_have_no_public_setters(String name) =>
        ArchModules
            .Get(name)
            .GetTypesIn("Domain")
            .SelectMany(Mutability.GetPublicSetters)
            .ShouldBeEmpty();

    [Theory]
    [EveryModule]
    public void Value_objects_cannot_be_changed_even_through_init(String name)
    {
        var valueObjectTypes = ArchModules
            .Get(name).Types
            .Where(type => type.Implements<IValueObject>())
            .ToList();

        Assert.SkipWhen(valueObjectTypes.Count == 0, $"{name} has no value objects");

        valueObjectTypes
            .SelectMany(type => Mutability.GetMutableMembers(type, allowInit: false))
            .ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Value_objects_hide_the_unchecked_factory(String name)
    {
        var valueObjectTypes = ArchModules
            .Get(name).Types
            .Where(type => type.Implements<IValueObject>())
            .ToList();

        Assert.SkipWhen(valueObjectTypes.Count == 0, $"{name} has no value objects");

        valueObjectTypes
            .Where(ValueObjectShape.HasPublicFrom)
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Theory]
    [EveryModule]
    public void Aggregates_entities_and_value_objects_are_created_through_their_factories(String name)
    {
        var modelTypes = ArchModules
            .Get(name).Types
            .Where(IsModelType)
            .ToList();

        Assert.SkipWhen(modelTypes.Count == 0, $"{name} has no aggregates, entities or value objects");

        modelTypes
            .SelectMany(GetOpenConstructors)
            .ShouldBeEmpty("Make the constructor private and create through a factory that returns Result");
    }

    [Theory]
    [EveryModule]
    public void Events_commands_and_queries_are_immutable(String name) =>
        ArchModules
            .Get(name).Types
            .Where(IsMessage)
            .SelectMany(type => Mutability.GetMutableMembers(type, allowInit: true))
            .ShouldBeEmpty();

    [Theory]
    [EveryModule]
    public void Aggregates_reference_other_aggregates_only_by_id(String name) =>
        ArchModules
            .Get(name).Types
            .Implementing<IAggregateRoot>()
            .SelectMany(GetOtherAggregatesReferencedBy)
            .ShouldBeEmpty();

    [Theory]
    [EveryModule]
    public void Domain_never_reads_the_clock_or_creates_random_ids(String name)
    {
        var module = ArchModules.Get(name);
        var domainTypeNames = module
            .GetTypesIn("Domain")
            .Select(type => type.FullName)
            .ToHashSet();

        var violations = module.Assemblies
            .Select(assembly => assembly.Location)
            .Distinct()
            .SelectMany(location => ReadForbiddenCallsIn(location, domainTypeNames))
            .ToList();

        violations.ShouldBeEmpty();
    }

    private static Boolean IsModelType(Type type) =>
        type.IsConcrete
            && !type.Implements(typeof(ITypedId<>))
            && (type.Implements<IAggregateRoot>()
                || type.DerivesFrom(typeof(Entity<>))
                || type.Implements<IValueObject>());

    private static IEnumerable<String> GetOpenConstructors(Type type) =>
        type
            .GetConstructors(InstanceMembers)
            .Where(constructor => !constructor.IsPrivate && !IsCopyConstructor(constructor, type))
            .Select(constructor => $"{type.FullName} has a {Describe(constructor)} constructor");

    private static Boolean IsCopyConstructor(ConstructorInfo constructor, Type type) =>
        type.IsRecord && constructor.GetParameters() is [{ } parameter] && parameter.ParameterType == type;

    private static String Describe(ConstructorInfo constructor) =>
        constructor.IsPublic ? "public" : constructor.IsFamily ? "protected" : "internal";

    private static Boolean IsMessage(Type type) =>
        type.Implements<DomainEvent>()
            || type.Implements<IntegrationEvent>()
            || type.Implements(typeof(ICommand<>))
            || type.Implements(typeof(IQuery<>));

    private static IEnumerable<String> GetOtherAggregatesReferencedBy(Type aggregateType) =>
        GetMemberTypes(aggregateType)
            .SelectMany(Expand)
            .Where(memberType => memberType.Implements<IAggregateRoot>() && memberType != aggregateType)
            .Select(memberType => $"{aggregateType.FullName} holds {memberType.FullName}");

    private static IEnumerable<Type> GetMemberTypes(Type type) => [.. GetFieldTypes(type), .. GetPropertyTypes(type)];

    private static IEnumerable<Type> GetFieldTypes(Type type) =>
        type
            .GetFields(InstanceMembers)
            .Select(field => field.FieldType);

    private static IEnumerable<Type> GetPropertyTypes(Type type) =>
        type
            .GetProperties(InstanceMembers)
            .Select(property => property.PropertyType);

    private static IEnumerable<Type> Expand(Type type) =>
        type.IsArray
            ? [type, .. Expand(type.GetElementType()!)]
            : type.IsGenericType
                ? [type, .. ExpandGenericArguments(type)]
                : [type];

    private static IEnumerable<Type> ExpandGenericArguments(Type type) =>
        type
            .GetGenericArguments()
            .SelectMany(Expand);

    private static List<String> ReadForbiddenCallsIn(String location, HashSet<String?> domainTypeNames)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(location);
        return assembly.MainModule
            .GetTypes()
            .Where(type => domainTypeNames.Contains(GetTopLevelType(type).FullName))
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .SelectMany(GetCallsFrom)
            .Where(call => ForbiddenCalls.Contains(BuildSignature(call.CalledMethod)))
            .Select(Describe)
            .ToList();
    }

    private static IEnumerable<(MethodDefinition CallingMethod, MethodReference CalledMethod)> GetCallsFrom(
        MethodDefinition method) =>
        method.Body.Instructions
            .Select(instruction => instruction.Operand)
            .OfType<MethodReference>()
            .Select(calledMethod => (method, calledMethod));

    private static String Describe((MethodDefinition CallingMethod, MethodReference CalledMethod) call)
    {
        var callingMethodName = $"{call.CallingMethod.DeclaringType.FullName}::{call.CallingMethod.Name}";
        return $"{callingMethodName} calls {BuildSignature(call.CalledMethod)}";
    }

    private static String BuildSignature(MethodReference method) =>
        $"{method.DeclaringType.FullName}::{method.Name}/{method.Parameters.Count}";

    private static TypeDefinition GetTopLevelType(TypeDefinition type) =>
        type.DeclaringType is null ? type : GetTopLevelType(type.DeclaringType);
}
