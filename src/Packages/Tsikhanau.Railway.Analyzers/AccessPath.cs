using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Tsikhanau.Railway.Analyzers;

internal sealed class AccessPath : IEquatable<AccessPath>
{
    private readonly ImmutableArray<ISymbol> _symbols;

    private AccessPath(ImmutableArray<ISymbol> symbols) => _symbols = symbols;

    public ISymbol Root => _symbols[0];

    public static AccessPath? From(IOperation? operation) => operation switch
    {
        ILocalReferenceOperation local => new AccessPath(ImmutableArray.Create<ISymbol>(local.Local)),
        IParameterReferenceOperation parameter => new AccessPath(ImmutableArray.Create<ISymbol>(parameter.Parameter)),
        IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance, Type: { } type } =>
            new AccessPath(ImmutableArray.Create<ISymbol>(type)),
        IFieldReferenceOperation field => Append(field.Instance, field.Field),
        IPropertyReferenceOperation { Arguments.IsEmpty: true } property => Append(property.Instance, property.Property),
        _ => null
    };

    public Boolean StartsWith(AccessPath prefix)
    {
        if (prefix._symbols.Length > _symbols.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix._symbols.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(_symbols[i], prefix._symbols[i]))
            {
                return false;
            }
        }

        return true;
    }

    public Boolean Equals(AccessPath? other) =>
        other is not null && other._symbols.Length == _symbols.Length && StartsWith(other);

    public override Boolean Equals(Object? obj) => obj is AccessPath other && Equals(other);

    public override Int32 GetHashCode()
    {
        var hash = 17;
        foreach (var symbol in _symbols)
        {
            hash = unchecked(hash * 31 + SymbolEqualityComparer.Default.GetHashCode(symbol));
        }

        return hash;
    }

    private static AccessPath? Append(IOperation? instance, ISymbol member)
    {
        if (instance is null)
        {
            return new AccessPath(ImmutableArray.Create(member));
        }

        return From(instance) is { } parent ? new AccessPath(parent._symbols.Add(member)) : null;
    }
}
