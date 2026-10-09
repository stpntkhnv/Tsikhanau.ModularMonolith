using System.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Support;

internal static class AssemblyExtensions
{
    private const String EmbeddedAttributeName = "Microsoft.CodeAnalysis.EmbeddedAttribute";

    extension(Assembly assembly)
    {
        public String Name => assembly.GetName().Name!;

        public IEnumerable<Type> HandwrittenTypes =>
            assembly
                .GetTypes()
                .Where(type => !IsGenerated(type));
    }

    private static Boolean IsGenerated(Type type) =>
        HasCompilerName(type) || IsEmbeddedByGenerator(type) || IsNestedInGenerated(type);

    private static Boolean HasCompilerName(Type type) => type.Name.StartsWith('<');

    private static Boolean IsEmbeddedByGenerator(Type type) =>
        type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == EmbeddedAttributeName);

    private static Boolean IsNestedInGenerated(Type type) =>
        type.DeclaringType is { } declaringType && IsGenerated(declaringType);
}
