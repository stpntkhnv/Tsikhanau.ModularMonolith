using System.Reflection;
using Tsikhanau.Monolith.ArchRules.Inputs;
using Tsikhanau.Monolith.ArchRules.Modules;
using Tsikhanau.Monolith.ArchRules.Tests.Fixtures;
using Tsikhanau.Monolith.ArchRules.Tests.Inputs;

namespace Tsikhanau.Monolith.ArchRules.Tests.Modules;

public sealed class NullabilityTests : NullabilityTests<TestModuleInputs>
{
    private const String ParcelsRootNamespace = "Tsikhanau.Monolith.Parcels";

    private const String ParcelShippedIntegrationEventName =
        $"{ParcelsRootNamespace}.Contracts.IntegrationEvents.ParcelShippedIntegrationEvent";

    private const String ShipParcelCommandName = $"{ParcelsRootNamespace}.Features.ShipParcel.ShipParcelCommand";

    private const String ShipParcelRequestName = $"{ParcelsRootNamespace}.Features.ShipParcel.ShipParcelRequest";

    private const String TrackParcelInternalCommandName =
        $"{ParcelsRootNamespace}.Features.TrackParcel.TrackParcelInternalCommand";

    private const String ParcelsSource = """
        using System;
        using Tsikhanau.Monolith.Application.Commands;
        using Tsikhanau.Monolith.Contracts.IntegrationEvents;
        using Tsikhanau.Monolith.Contracts.Queries;
        using Tsikhanau.Monolith.Domain.Events;
        using Tsikhanau.Railway;

        namespace Tsikhanau.Monolith.Parcels.Contracts.IntegrationEvents
        {
            public sealed record ParcelShippedIntegrationEvent(
                DateTimeOffset OccurredOn,
                Guid ParcelId,
                String? Carrier)
                : IntegrationEvent(OccurredOn);
        }

        namespace Tsikhanau.Monolith.Parcels.Contracts.Queries.GetParcel
        {
            public sealed record GetParcelQuery(Guid ParcelId, String? Carrier) : IQuery<ParcelDto>;

            public sealed record ParcelDto(Guid Id, String? Carrier);
        }

        namespace Tsikhanau.Monolith.Parcels.Domain.Parcels.Events
        {
            public sealed record ParcelShippedDomainEvent(DateTimeOffset OccurredOn, Guid ParcelId, String? Note)
                : DomainEvent(OccurredOn);
        }

        namespace Tsikhanau.Monolith.Parcels.Features.ShipParcel
        {
            internal sealed record ShipParcelCommand(Guid ParcelId, String? Note) : ICommand<Unit>;

            internal sealed record ShipParcelRequest
            {
                public required Guid ParcelId { get; init; }

                public String? Note { get; init; }
            }
        }

        namespace Tsikhanau.Monolith.Parcels.Features.TrackParcel
        {
            internal sealed record TrackParcelInternalCommand(Guid Id, Guid ParcelId, Int32? Attempt)
                : InternalCommand(Id);
        }

        namespace Tsikhanau.Monolith.Parcels.Features.GetParcel
        {
            internal sealed record ParcelRow(Guid Id, String? Carrier);
        }

        namespace Tsikhanau.Monolith.Parcels.Infrastructure.Carriers
        {
            internal sealed record CarrierRequest(String? Token);
        }
        """;

    private static readonly Lazy<Assembly> ParcelsAssembly = new(LoadParcelsAssembly);

    private static ArchModule ParcelsModule => new("Parcels", ParcelsRootNamespace, () => [ParcelsAssembly.Value]);

    [Fact]
    public void Messages_and_requests_rule_checks_integration_events_commands_and_requests_in_features()
    {
        var evaluation = NullabilityRules.MessagesAndRequestsCarryOptionalInsteadOfNull(ParcelsModule);

        evaluation.Subject.ShouldBe(
            [
                ParcelShippedIntegrationEventName,
                ShipParcelCommandName,
                ShipParcelRequestName,
                TrackParcelInternalCommandName,
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Messages_and_requests_rule_names_every_nullable_member_of_what_it_checks()
    {
        var evaluation = NullabilityRules.MessagesAndRequestsCarryOptionalInsteadOfNull(ParcelsModule);

        evaluation.Violations.ShouldBe(
            [
                $"{ParcelShippedIntegrationEventName}.Carrier",
                $"{ShipParcelCommandName}.Note",
                $"{ShipParcelRequestName}.Note",
                $"{TrackParcelInternalCommandName}.Attempt",
            ],
            ignoreOrder: true);
    }

    private static Assembly LoadParcelsAssembly() =>
        Assembly.Load(FixtureCompiler.Compile(ParcelsRootNamespace, ParcelsSource, FixtureCompiler.RuntimeReferences));
}
