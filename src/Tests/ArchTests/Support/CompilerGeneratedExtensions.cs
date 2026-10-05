using System.Reflection;
using System.Runtime.CompilerServices;

namespace ModularMonolith.ArchTests.Support;

public static class CompilerGeneratedExtensions
{
    extension(Type type)
    {
        public Boolean IsCompilerGenerated =>
            type.Name.StartsWith('<')
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
