namespace Tsikhanau.Railway;

public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = default;

    public override String ToString() => "()";

    public Boolean Equals(Unit other) => true;

    public override Boolean Equals(Object? obj) => obj is Unit;

    public override Int32 GetHashCode() => 0;

    public static Boolean operator ==(Unit left, Unit right) => true;

    public static Boolean operator !=(Unit left, Unit right) => false;
}