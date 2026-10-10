using Tsikhanau.Monolith.Application.Queries;
using Tsikhanau.Monolith.TestModules.Pings.Domain.Pings;
using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.TestModules.Pings.Features.ProduceError;

internal sealed class ProduceErrorQueryHandler : IQueryHandler<ProduceErrorQuery, Unit>
{
    private static readonly Error LabelTooLong = Error.Validation("label", "Label is too long");

    private static readonly Error NicknameEmpty = Error.Validation("nickname", "Nickname is empty");

    private static readonly Error BadShape = new(ErrorKind.Validation, "Ping.BadShape", "Ping has a bad shape");

    private static readonly Error Exploded = Error.Unexpected("Ping.Exploded", "Ping exploded inside the handler");

    private static readonly Error RejectedForItsLabel = LabelTooLong
        .WithContext("Ping.Invalid", "Ping is not valid")
        .WithContext("Ping.Rejected", "Ping was rejected");

    private static readonly Error RejectedForItsValues = AggregateError
        .From([LabelTooLong, NicknameEmpty])
        .WithContext("Ping.Rejected", "Ping was rejected");

    public Task<Result<Unit>> Handle(ProduceErrorQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Produce(query.Kind));

    private static Result<Unit> Produce(ProducedErrorKind kind) =>
        kind switch
        {
            ProducedErrorKind.ValidationWithFields => ValidationError.From(
            [
                new FieldError("label", "Label is too long"),
                new FieldError("label", "Label has spaces"),
                new FieldError("nickname", "Nickname is empty")
            ]),
            ProducedErrorKind.ValidationWithoutField => BadShape,
            ProducedErrorKind.AggregateOfTwoValidations => AggregateError.From([LabelTooLong, NicknameEmpty]),
            ProducedErrorKind.AggregateOfFieldAndFieldlessValidations => AggregateError.From([LabelTooLong, BadShape]),
            ProducedErrorKind.ValidationWithContext => RejectedForItsLabel,
            ProducedErrorKind.AggregateWithContext => RejectedForItsValues,
            ProducedErrorKind.Unauthorized => Error.Unauthorized("Ping.Unauthorized", "Caller is not signed in"),
            ProducedErrorKind.Forbidden => Error.Forbidden("Ping.Forbidden", "Caller may not see this ping"),
            ProducedErrorKind.NotFound => PingErrors.NotFound,
            ProducedErrorKind.Conflict => PingErrors.TooManyTouches,
            ProducedErrorKind.Failure => Error.Failure("Ping.NotProcessed", "Ping could not be processed"),
            ProducedErrorKind.Unexpected => Exploded,
            ProducedErrorKind.AggregateHoldingUnexpected => AggregateError.From([PingErrors.TooManyTouches, Exploded]),
            _ => Unit.Value
        };
}
