using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ModularMonolith.ArchTests.Support;

public static partial class CompilerGeneratedExtensions
{
    [GeneratedRegex("^<[^>]+>F[0-9A-F]+__")]
    private static partial Regex FileLocalTypeName { get; }

    extension(Type type)
    {
        public Boolean IsCompilerGenerated =>
            (type.Name.StartsWith('<') && !FileLocalTypeName.IsMatch(type.Name))
                || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                || type.DeclaringType is { IsCompilerGenerated: true };

        public Type TopLevelType => type.DeclaringType is { } declaringType ? declaringType.TopLevelType : type;

        public Boolean IsRecord => type.GetMethod("<Clone>$") is not null;
    }

    extension(MemberInfo member)
    {
        public Boolean IsCompilerGenerated =>
            member.Name.StartsWith('<') || member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
    }
}
