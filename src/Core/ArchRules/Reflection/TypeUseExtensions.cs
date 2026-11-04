using System.Reflection;

namespace Tsikhanau.Monolith.ArchRules.Reflection;

internal static class TypeUseExtensions
{
    private const BindingFlags DeclaredMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    extension(Type type)
    {
        public IEnumerable<TypeUse> ParameterUses =>
            GetDeclaredMethods(type)
                .SelectMany(method => method.GetParameters())
                .Select(parameter => CreateParameterUse(parameter.Member, parameter));

        public IEnumerable<TypeUse> ReturnUses =>
            type
                .GetMethods(DeclaredMembers)
                .Select(CreateReturnUse);

        public IEnumerable<TypeUse> FieldUses =>
            type
                .GetFields(DeclaredMembers)
                .Select(CreateFieldUse);

        public IEnumerable<TypeUse> PropertyUses =>
            type
                .GetProperties(DeclaredMembers)
                .Select(CreatePropertyUse);

        public IEnumerable<TypeUse> IndexParameterUses =>
            type
                .GetProperties(DeclaredMembers)
                .SelectMany(GetIndexParameterUses);

        public IEnumerable<TypeUse> LocalUses =>
            GetDeclaredMethods(type)
                .SelectMany(GetLocalUses);
    }

    private static IEnumerable<MethodBase> GetDeclaredMethods(Type type) =>
        [.. type.GetConstructors(DeclaredMembers), .. type.GetMethods(DeclaredMembers)];

    private static TypeUse CreateParameterUse(MemberInfo member, ParameterInfo parameter) =>
        new(member, "takes", parameter.ParameterType, parameter.Name ?? String.Empty);

    private static TypeUse CreateReturnUse(MethodInfo method) => new(method, "returns", method.ReturnType, method.Name);

    private static TypeUse CreateFieldUse(FieldInfo field) => new(field, "holds", field.FieldType, field.Name);

    private static TypeUse CreatePropertyUse(PropertyInfo property) =>
        new(property, "holds", property.PropertyType, property.Name);

    private static IEnumerable<TypeUse> GetIndexParameterUses(PropertyInfo property) =>
        property
            .GetIndexParameters()
            .Select(parameter => CreateParameterUse(property, parameter));

    private static IEnumerable<TypeUse> GetLocalUses(MethodBase method)
    {
        var place = $"in {method.DeclaringType?.Name}.{method.Name}";
        return (method.GetMethodBody()?.LocalVariables ?? [])
            .Select(local => new TypeUse(method, "uses", local.LocalType, place));
    }
}
