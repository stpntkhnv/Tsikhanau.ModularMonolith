namespace ModularMonolith.BuildingBlocks.Contracts.IntegrationEvents;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventNameAttribute(String name) : Attribute
{
    public String Name { get; } = name;
}
