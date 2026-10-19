using System.Diagnostics;
using System.Reflection;
using Mono.Cecil;
using Tsikhanau.Monolith.Application.Commands;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Tsikhanau.Monolith.ArchRules.Support;
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Monolith.Contracts.Queries;
using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Monolith.Domain.Ids;
using Tsikhanau.Monolith.Domain.ValueObjects;
using Tsikhanau.Monolith.Shared.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public static class DomainRules
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private const BindingFlags DeclaredMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

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
        .. ReadForbiddenReferences(module, domainTypes),
        .. domainTypes
            .Where(type => !type.IsCompilerGenerated)
            .SelectMany(GetClockTypedMembers),
    ];

    private static IEnumerable<String> ReadForbiddenReferences(ArchModule module, IReadOnlyList<Type> domainTypes)
    {
        var domainTypeNames = domainTypes
            .Select(type => type.FullName)
            .ToHashSet();

        return module.Assemblies
            .Select(assembly => assembly.Location)
            .Distinct()
            .SelectMany(location => ReadForbiddenReferencesIn(location, domainTypeNames));
    }

    private static List<String> ReadForbiddenReferencesIn(String location, HashSet<String?> domainTypeNames)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(location);
        return assembly.MainModule
            .GetTypes()
            .Where(type => domainTypeNames.Contains(GetTopLevelType(type).FullName))
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .SelectMany(GetForbiddenReferencesFrom)
            .ToList();
    }

    private static IEnumerable<String> GetForbiddenReferencesFrom(MethodDefinition method)
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
        GetSignatureTypes(type)
            .Where(signatureType => IsClockOrDerived(signatureType.Type))
            .Select(signatureType => signatureType.Describe(type));

    private static Boolean IsClockOrDerived(Type type) => ClockTypes.Any(type.IsAssignableTo);

    private static IEnumerable<SignatureType> GetSignatureTypes(Type type)
    {
        IReadOnlyList<PropertyInfo> properties = type.GetProperties(DeclaredMembers);
        HashSet<MethodInfo> accessors = [.. properties.SelectMany(property => property.GetAccessors(nonPublic: true))];
        IReadOnlyList<MethodBase> methods =
        [
            .. type.GetConstructors(DeclaredMembers),
            .. type
                .GetMethods(DeclaredMembers)
                .Where(method => !accessors.Contains(method) && !method.IsCompilerGenerated),
        ];

        var parameters = methods
            .SelectMany(method => method.GetParameters())
            .Select(parameter => new SignatureType(parameter.Member, "takes", parameter.ParameterType));

        var returns = methods
            .OfType<MethodInfo>()
            .Select(method => new SignatureType(method, "returns", method.ReturnType));

        var fields = type
            .GetFields(DeclaredMembers)
            .Where(field => !field.IsCompilerGenerated)
            .Select(field => new SignatureType(field, "holds", field.FieldType));

        var propertyTypes = properties.Select(property => new SignatureType(property, "holds", property.PropertyType));

        var indexParameters = properties.SelectMany(GetIndexParameterTypes);

        return [.. parameters, .. returns, .. fields, .. propertyTypes, .. indexParameters];
    }

    private static IEnumerable<SignatureType> GetIndexParameterTypes(PropertyInfo property) =>
        property
            .GetIndexParameters()
            .Select(parameter => new SignatureType(property, "takes", parameter.ParameterType));

    private static String BuildSignature(MethodReference method) =>
        $"{method.DeclaringType.FullName}::{method.Name}/{method.Parameters.Count}";

    private static TypeDefinition GetTopLevelType(TypeDefinition type) =>
        type.DeclaringType is null ? type : GetTopLevelType(type.DeclaringType);

    private sealed record SignatureType(MemberInfo Member, String Use, Type Type)
    {
        public String Describe(Type owner) => $"{owner.FullName}::{Member.Name} {Use} {Type.FullName}";
    }
}
