namespace Micro.CQRS.Core;

public readonly struct Unit : IEquatable<Unit>
{
    private static readonly Unit _value = new();

    public static Unit Value => _value;

    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";

    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
}
