namespace ModularMonolith.BuildingBlocks.Application.Commands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class InternalCommandNameAttribute(String name) : Attribute
{
    public String Name { get; } = name;
}
