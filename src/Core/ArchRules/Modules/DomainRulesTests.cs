using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Rules;
using Xunit;

namespace Tsikhanau.Monolith.ArchRules.Modules;

public abstract class DomainRulesTests<TInputs> : ArchRuleTests<TInputs>
    where TInputs : IArchRuleInputs
{
    [Theory]
    [EveryModule]
    public void Domain_types_have_no_public_setters(String name) =>
        Check(
            DomainRules.DomainTypesHaveNoPublicSetters(GetModule(name)),
            $"{name} has no types in Domain",
            "Make the setter private and change the state through a method of the type");

    [Theory]
    [EveryModule]
    public void Value_objects_cannot_be_changed_even_through_init(String name) =>
        Check(
            DomainRules.ValueObjectsCannotBeChangedEvenThroughInit(GetModule(name)),
            $"{name} has no value objects",
            "Drop the init accessor; a value object gets its values only through its factory");

    [Theory]
    [EveryModule]
    public void Value_objects_hide_the_unchecked_factory(String name) =>
        Check(
            DomainRules.ValueObjectsHideTheUncheckedFactory(GetModule(name)),
            $"{name} has no value objects",
            "Implement the unchecked From explicitly as IValueObject<TSelf, TValue>.From; callers go through the "
            + "factory that returns Result");

    [Theory]
    [EveryModule]
    public void Aggregates_entities_and_value_objects_are_created_through_their_factories(String name) =>
        Check(
            DomainRules.AggregatesEntitiesAndValueObjectsAreCreatedThroughTheirFactories(GetModule(name)),
            $"{name} has no aggregates, entities or value objects",
            "Make the constructor private and create through a factory that returns Result");

    [Theory]
    [EveryModule]
    public void Events_commands_and_queries_are_immutable(String name) =>
        Check(
            DomainRules.EventsCommandsAndQueriesAreImmutable(GetModule(name)),
            $"{name} has no events, commands or queries",
            "Give events, commands and queries get-only properties that the constructor sets");

    [Theory]
    [EveryModule]
    public void Aggregates_reference_other_aggregates_only_by_id(String name) =>
        Check(
            DomainRules.AggregatesReferenceOtherAggregatesOnlyById(GetModule(name)),
            $"{name} has no aggregates",
            "Hold the other aggregate's id, not the aggregate itself");

    [Theory]
    [EveryModule]
    public void Domain_never_reads_the_clock_or_creates_random_ids(String name) =>
        Check(
            DomainRules.DomainNeverReadsTheClockOrCreatesRandomIds(GetModule(name)),
            $"{name} has no types in Domain",
            "Take the moment as a parameter from the handler's TimeProvider; make ids with Guid.CreateVersion7");
}
