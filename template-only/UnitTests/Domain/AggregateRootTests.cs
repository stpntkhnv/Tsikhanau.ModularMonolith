using Tsikhanau.Monolith.Domain.Aggregates;
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Monolith.Domain.Ids;

namespace Tsikhanau.Monolith.UnitTests.Domain;

public sealed class AggregateRootTests
{
    private static readonly DateTimeOffset CreatedOn = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Raised_events_stay_until_cleared()
    {
        IAggregateRoot aggregateRoot = SampleAggregate.Create(SampleId.New(CreatedOn), CreatedOn);

        aggregateRoot.DomainEvents
            .ShouldHaveSingleItem()
            .ShouldBeOfType<SampleCreatedDomainEvent>();

        aggregateRoot.ClearDomainEvents();

        aggregateRoot.DomainEvents.ShouldBeEmpty();
    }

    private readonly record struct SampleId(Guid Value) : ITypedId<SampleId>
    {
        public static SampleId New(DateTimeOffset createdOn) => new(Guid.CreateVersion7(createdOn));

        public static SampleId From(Guid value) => new(value);
    }

    private sealed record SampleCreatedDomainEvent(DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

    private sealed class SampleAggregate : AggregateRoot<SampleId>
    {
        private SampleAggregate(SampleId id) : base(id) { }

        public static SampleAggregate Create(SampleId id, DateTimeOffset createdOn)
        {
            var aggregate = new SampleAggregate(id);
            aggregate.Raise(new SampleCreatedDomainEvent(createdOn));
            return aggregate;
        }
    }
}
