using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tsikhanau.Monolith.Scaffold.Modules;
using Tsikhanau.Monolith.Scaffold.Sources;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Scaffold.Events;

internal sealed class MemberSamples(SourceProject project)
{
    public const String SupportedTypes =
        "supported member types: DateTimeOffset, DateOnly, Guid, String, Int32, Int64, Decimal, Double, Boolean, "
        + "an array, IReadOnlyList<T>, IReadOnlyCollection<T>, an enum, a typed id (ITypedId<T>) or a value object "
        + "(IValueObject<T, TValue>) declared in the event's project, and Optional<T> whose T is one of the first "
        + "nine or declared there";

    private const String Clock = "App.Time.GetUtcNow()";

    private const String FactoryNamespace = "Tsikhanau.Monolith.Infrastructure.Persistence";

    private const String RailwayNamespace = "Tsikhanau.Railway";

    private const String EmptyCollection = "[]";

    private static readonly String[] Collections = ["IReadOnlyList", "IReadOnlyCollection"];

    private static readonly String[] Numbers = ["Int32", "Int64", "Decimal", "Double"];

    private static readonly String[] Primitives =
        ["DateTimeOffset", "DateOnly", "Guid", "String", "Boolean", .. Numbers];

    public Optional<MemberSample> Build(ParameterSyntax parameter) =>
        parameter.Type is { } type ? BuildSample(type, parameter.Identifier.Text) : Optional<MemberSample>.None();

    private Optional<MemberSample> BuildSample(TypeSyntax type, String memberName) =>
        type switch
        {
            ArrayTypeSyntax => MemberSample.Of(EmptyCollection),
            NameSyntax name when IsOptional(name) => BuildNone(name.TypeArguments[0]),
            NameSyntax name when IsCollection(name) => MemberSample.Of(EmptyCollection),
            NameSyntax name when IsPlain(name) => BuildPrimitive(name.SimpleName, memberName)
                .OrElse(() => BuildDomainSample(name.SimpleName, memberName)),
            _ => Optional<MemberSample>.None(),
        };

    private static Boolean IsPlain(TypeSyntax type) => type is NameSyntax && type.TypeArguments.Count == 0;

    private static Boolean IsOptional(NameSyntax name) =>
        name.SimpleName == "Optional" && name.TypeArguments.Count == 1;

    private static Boolean IsCollection(NameSyntax name) =>
        Collections.Contains(name.SimpleName) && name.TypeArguments.Count == 1;

    private static Optional<MemberSample> BuildPrimitive(String typeName, String memberName) =>
        typeName switch
        {
            "DateTimeOffset" => MemberSample.Of(Clock),
            "DateOnly" => MemberSample.Of($"DateOnly.FromDateTime({Clock}.UtcDateTime)"),
            "Guid" => MemberSample.Of($"Guid.CreateVersion7({Clock})"),
            "String" => MemberSample.Of($"\"{memberName.ToKebabCase()}\""),
            "Boolean" => MemberSample.Of("true"),
            _ when Numbers.Contains(typeName) => MemberSample.Of("1"),
            _ => Optional<MemberSample>.None(),
        };

    private Optional<MemberSample> BuildNone(TypeSyntax valueType) =>
        FindValueTypeUsings(valueType)
            .Map(usings => MemberSample.Of($"Optional<{valueType}>.None()", [RailwayNamespace, .. usings]));

    private Optional<String[]> FindValueTypeUsings(TypeSyntax valueType) =>
        valueType switch
        {
            _ when !IsPlain(valueType) => Optional<String[]>.None(),
            _ when Primitives.Contains(valueType.SimpleName) => Optional<String[]>.Some([]),
            _ => project
                .FindDeclaration<BaseTypeDeclarationSyntax>(valueType.SimpleName)
                .Map(declaration => new[] { declaration.Namespace }),
        };

    private Optional<MemberSample> BuildDomainSample(String typeName, String memberName) =>
        project
            .FindDeclaration<BaseTypeDeclarationSyntax>(typeName)
            .Bind(declaration => declaration switch
            {
                EnumDeclarationSyntax { Members: [var firstMember, ..] } => MemberSample.Of(
                    $"{typeName}.{firstMember.Identifier.Text}",
                    declaration.Namespace),
                TypeDeclarationSyntax typeDeclaration => BuildTypedIdOrValueObject(typeDeclaration, memberName),
                _ => Optional<MemberSample>.None(),
            });

    private static Optional<MemberSample> BuildTypedIdOrValueObject(
        TypeDeclarationSyntax declaration,
        String memberName) =>
        declaration.BaseTypes.Any(IsTypedId)
            ? MemberSample.Of(
                $"TypedIdFactory<{declaration.Name}>.Create(Guid.CreateVersion7({Clock}))",
                FactoryNamespace,
                declaration.Namespace)
            : FindValueType(declaration)
                .Bind(valueType => BuildPrimitive(valueType.SimpleName, memberName)
                    .Map(value => MemberSample.Of(
                        $"ValueObjectFactory<{declaration.Name}, {valueType}>.Create({value.Expression})",
                        FactoryNamespace,
                        declaration.Namespace)));

    private static Boolean IsTypedId(TypeSyntax baseType) => baseType.SimpleName == "ITypedId";

    private static Boolean IsValueObject(TypeSyntax baseType) =>
        baseType.SimpleName == "IValueObject" && baseType.TypeArguments.Count == 2;

    private static Optional<TypeSyntax> FindValueType(TypeDeclarationSyntax declaration) =>
        declaration.BaseTypes
            .Where(IsValueObject)
            .Select(baseType => baseType.TypeArguments[1])
            .FirstOrDefault();
}
