using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace ModularMonolith.ArchTests.Support;

public static partial class CompilerGeneratedExtensions
{
    [GeneratedRegex("^<[^>]+>F[0-9A-F]+__")]
    private static partial Regex FileLocalTypeName { get; }

    extension(Type type)
    {
        public Boolean IsCompilerGenerated =>
            HasCompilerName(type.Name)
                || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                || type.DeclaringType is { IsCompilerGenerated: true };

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
            HasCompilerName(type.Name)
                || type.CustomAttributes.Any(IsCompilerGeneratedAttribute)
                || type.DeclaringType is { IsCompilerGenerated: true };
    }

    extension(MemberInfo member)
    {
        public Boolean IsCompilerGenerated =>
            member.Name.StartsWith('<') || member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
    }

    private static Boolean HasCompilerName(String typeName) =>
        typeName.StartsWith('<') && !FileLocalTypeName.IsMatch(typeName);

    private static Boolean IsCompilerGeneratedAttribute(CustomAttribute attribute) =>
        attribute.AttributeType.FullName == typeof(CompilerGeneratedAttribute).FullName;
}
