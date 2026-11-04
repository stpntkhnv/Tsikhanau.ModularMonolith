using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace Tsikhanau.Monolith.ArchRules.Reflection;

internal static partial class CompilerGeneratedExtensions
{
    private const String EmbeddedAttributeName = "Microsoft.CodeAnalysis.EmbeddedAttribute";

    [GeneratedRegex("^<[^>]+>F[0-9A-F]+__")]
    private static partial Regex FileLocalTypeName { get; }

    extension(Type type)
    {
        public Boolean IsCompilerGenerated =>
            HasNameOfAGeneratedType(type.Name)
                || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                || type.DeclaringType is { IsCompilerGenerated: true };

        public Boolean IsGeneratedOrFileLocal =>
            HasCompilerName(type.Name)
                || IsEmbeddedByGenerator(type)
                || type.DeclaringType is { IsGeneratedOrFileLocal: true };

        public Type TopLevelType => type.DeclaringType is { } declaringType ? declaringType.TopLevelType : type;

        public IEnumerable<Type> WithCompilerGeneratedNestedTypes =>
            type
                .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(nestedType => nestedType.IsCompilerGenerated)
                .SelectMany(nestedType => nestedType.WithCompilerGeneratedNestedTypes)
                .Prepend(type);

        public Boolean IsRecord => type.GetMethod("<Clone>$") is not null;
    }

    extension(TypeDefinition type)
    {
        public Boolean IsCompilerGenerated =>
            HasNameOfAGeneratedType(type.Name)
                || type.CustomAttributes.Any(IsCompilerGeneratedAttribute)
                || type.DeclaringType is { IsCompilerGenerated: true };
    }

    extension(MemberInfo member)
    {
        public Boolean IsCompilerGenerated =>
            HasCompilerName(member.Name) || member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
    }

    extension(Assembly assembly)
    {
        public IEnumerable<Type> HandwrittenTypes =>
            assembly
                .GetTypes()
                .Where(type => !type.IsGeneratedOrFileLocal);
    }

    private static Boolean HasCompilerName(String name) => name.StartsWith('<');

    private static Boolean HasNameOfAGeneratedType(String typeName) =>
        HasCompilerName(typeName) && !FileLocalTypeName.IsMatch(typeName);

    private static Boolean IsEmbeddedByGenerator(Type type) =>
        type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == EmbeddedAttributeName);

    private static Boolean IsCompilerGeneratedAttribute(CustomAttribute attribute) =>
        attribute.AttributeType.FullName == typeof(CompilerGeneratedAttribute).FullName;
}
