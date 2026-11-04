using System.Diagnostics;
using System.Reflection;
using Mono.Cecil;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.ArchRules.Cecil;
using Tsikhanau.Monolith.ArchRules.Evaluation;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Reflection;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Monolith.Domain.Ids;
using Tsikhanau.Monolith.Domain.ValueObjects;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules.TypeDesign;

public static class DomainRules
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly HashSet<Type> ClockTypes = [typeof(TimeProvider), typeof(Stopwatch)];

    private static readonly HashSet<String> ClockTypeNames = [.. ClockTypes.Select(type => type.FullNameOrName)];

    private static readonly HashSet<String> ForbiddenCalls =
    [
        "System.DateTime::get_UtcNow/0",
        "System.DateTime::get_Now/0",
        "System.DateTime::get_Today/0",
        "System.DateTimeOffset::get_UtcNow/0",
        "System.DateTimeOffset::get_Now/0",
        "System.Environment::get_TickCount/0",
        "System.Environment::get_TickCount64/0",
        "System.Guid::NewGuid/0",
        "System.Guid::CreateVersion7/0",
    ];

    public static RuleEvaluation DomainTypesHaveNoPublicSetters(ArchModule module) =>
        module
            .GetTypesIn("Domain")
            .CollectViolations(Mutability.GetPublicSetters);

    public static RuleEvaluation DomainTypesHaveNoPublicFields(ArchModule module) =>
        module
            .GetTypesIn("Domain")
            .Where(type => !type.IsCompilerGenerated)
            .CollectViolations(Mutability.GetPublicFields);

    public static RuleEvaluation ValueObjectsCannotBeChangedEvenThroughInit(ArchModule module) =>
        module.Types
            .Where(type => type.Implements<IValueObject>())
            .CollectViolations(type => Mutability.GetMutableMembers(type, allowInit: false));

    public static RuleEvaluation ValueObjectsHideTheUncheckedFactory(ArchModule module) =>
        module.Types
            .Where(type => type.Implements<IValueObject>())
            .CollectViolations(ValueObjectShape.HasPublicFrom);

    public static RuleEvaluation AggregatesEntitiesAndValueObjectsAreCreatedThroughTheirFactories(ArchModule module) =>
        module.Types
            .Where(IsModelType)
            .CollectViolations(GetOpenConstructors);

    public static RuleEvaluation EventsCommandsAndQueriesAreImmutable(ArchModule module) =>
        module.Types
            .Where(IsMessage)
            .CollectViolations(type => Mutability.GetMutableMembers(type, allowInit: true));

    public static RuleEvaluation AggregatesReferenceOtherAggregatesOnlyById(ArchModule module) =>
        module.Types
            .Implementing<IAggregateRoot>()
            .CollectViolations(GetOtherAggregatesReferencedBy);

    public static RuleEvaluation DomainNeverReadsTheClockOrCreatesRandomIds(ArchModule module)
    {
        IReadOnlyList<Type> domainTypes = [.. module.GetTypesIn("Domain")];
        return RuleEvaluation.Create(domainTypes, () => ReadClockUses(module, domainTypes));
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
            .SelectMany(memberType => memberType.WithWrappedTypes)
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

    private static IEnumerable<String> ReadClockUses(ArchModule module, IReadOnlyList<Type> domainTypes) =>
    [
        .. ReadForbiddenCallsAndClockReads(module, domainTypes),
        .. domainTypes
            .Where(type => !type.IsCompilerGenerated)
            .SelectMany(GetClockTypedMembers),
    ];

    private static IReadOnlyList<String> ReadForbiddenCallsAndClockReads(
        ArchModule module,
        IReadOnlyList<Type> domainTypes)
    {
        var domainTypeNames = domainTypes
            .Select(type => type.FullName)
            .ToHashSet();

        return CecilAssemblies.Read(
            module.Assemblies,
            type => domainTypeNames.Contains(type.TopLevelType.FullName),
            type => type.MethodsWithBodies.SelectMany(GetForbiddenCallsAndClockReads));
    }

    private static IEnumerable<String> GetForbiddenCallsAndClockReads(MethodDefinition method)
    {
        var methodName = $"{method.DeclaringType.FullName}::{method.Name}";
        IReadOnlyList<Object> operands = [.. method.Body.Instructions.Select(instruction => instruction.Operand)];

        var calls = operands
            .OfType<MethodReference>()
            .Where(IsForbidden)
            .Select(calledMethod => $"{methodName} calls {BuildSignature(calledMethod)}");

        var reads = operands
            .OfType<FieldReference>()
            .Where(field => IsClockType(field.DeclaringType))
            .Select(field => $"{methodName} reads {field.DeclaringType.FullName}::{field.Name}");

        return [.. calls, .. reads];
    }

    private static Boolean IsForbidden(MethodReference calledMethod) =>
        ForbiddenCalls.Contains(BuildSignature(calledMethod)) || IsClockType(calledMethod.DeclaringType);

    private static Boolean IsClockType(TypeReference type) => ClockTypeNames.Contains(type.FullName);

    private static IEnumerable<String> GetClockTypedMembers(Type type) =>
        GetSignatureUses(type)
            .Where(typeUse => IsClockOrDerived(typeUse.Type))
            .Select(typeUse => $"{type.FullName}::{typeUse.Member.Name} {typeUse.Verb} {typeUse.Type.FullName}");

    private static Boolean IsClockOrDerived(Type type) => ClockTypes.Any(type.IsAssignableTo);

    private static IEnumerable<TypeUse> GetSignatureUses(Type type)
    {
        var accessors = GetAccessors(type);
        IEnumerable<TypeUse> methodUses = [.. type.ParameterUses, .. type.ReturnUses];

        return
        [
            .. methodUses.Where(typeUse => !IsAccessorOrGeneratedMethod(typeUse.Member, accessors)),
            .. type.FieldUses.Where(typeUse => !typeUse.Member.IsCompilerGenerated),
            .. type.PropertyUses,
            .. type.IndexParameterUses,
        ];
    }

    private static HashSet<MethodInfo> GetAccessors(Type type) =>
    [
        .. type
            .GetProperties(TypeUseExtensions.DeclaredMembers)
            .SelectMany(property => property.GetAccessors(nonPublic: true)),
    ];

    private static Boolean IsAccessorOrGeneratedMethod(MemberInfo member, HashSet<MethodInfo> accessors) =>
        member is MethodInfo method && (accessors.Contains(method) || method.IsCompilerGenerated);

    private static String BuildSignature(MethodReference method) =>
        $"{method.DeclaringType.FullName}::{method.Name}/{method.Parameters.Count}";
}
