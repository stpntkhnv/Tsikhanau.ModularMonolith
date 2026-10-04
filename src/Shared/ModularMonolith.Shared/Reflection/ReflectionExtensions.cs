using System.Reflection;

namespace ModularMonolith.Shared.Reflection;

public static class ReflectionExtensions
{
    extension(Assembly assembly)
    {
        public IEnumerable<Type> ConcreteTypes =>
            assembly
                .GetTypes()
                .Where(type => type.IsConcrete);

        public IEnumerable<T> CreateInstancesOf<T>() =>
            assembly
                .GetTypes()
                .Implementing<T>()
                .Select(type => type.CreateInstance<T>());
    }

    extension(IEnumerable<Type> types)
    {
        public IEnumerable<Type> Implementing<T>() => types.Where(type => type.IsConcrete && type.Implements<T>());
    }

    extension(Type type)
    {
        public Boolean IsConcrete => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false };

        public Boolean Implements<T>() => type.IsAssignableTo(typeof(T));

        public Boolean IsInNamespace(String rootNamespace) =>
            type.Namespace is { } typeNamespace
                && (typeNamespace == rootNamespace
                    || typeNamespace.StartsWith($"{rootNamespace}.", StringComparison.Ordinal));

        public Boolean Implements(Type openGeneric) =>
            type
                .GetInterfacesBuiltFrom(openGeneric)
                .Any();

        public IEnumerable<Type> GetInterfacesBuiltFrom(Type openGeneric) =>
            type
                .GetInterfaces()
                .Where(contract => contract.IsBuiltFrom(openGeneric));

        public Boolean IsBuiltFrom(Type openGeneric) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == openGeneric;

        public Boolean DerivesFrom(Type openGeneric) =>
            type.BaseType is { } baseType && (baseType.IsBuiltFrom(openGeneric) || baseType.DerivesFrom(openGeneric));

        public T CreateInstance<T>() => (T)Activator.CreateInstance(type, nonPublic: true)!;
    }
}
