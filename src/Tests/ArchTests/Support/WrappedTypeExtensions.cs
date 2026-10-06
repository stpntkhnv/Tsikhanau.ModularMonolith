using ModularMonolith.BuildingBlocks.Application.Modules;

namespace ModularMonolith.ArchTests.Support;

public static class WrappedTypeExtensions
{
    extension(Type type)
    {
        public IEnumerable<Type> WithWrappedTypes =>
            GetWrappedTypes(type)
                .SelectMany(wrappedType => wrappedType.WithWrappedTypes)
                .Prepend(type);
    }

    private static IEnumerable<Type> GetWrappedTypes(Type type) =>
        type.HasElementType ? [type.GetElementType()!] : GetTypeArgumentsExceptModuleNames(type);

    private static IEnumerable<Type> GetTypeArgumentsExceptModuleNames(Type type)
    {
        if (!type.IsConstructedGenericType)
        {
            return [];
        }

        var typeParameters = type
            .GetGenericTypeDefinition()
            .GetGenericArguments();

        return type
            .GetGenericArguments()
            .Where((_, index) => !NamesModule(typeParameters[index]));
    }

    private static Boolean NamesModule(Type typeParameter) =>
        typeParameter
            .GetGenericParameterConstraints()
            .Contains(typeof(IModuleFacade));
}
