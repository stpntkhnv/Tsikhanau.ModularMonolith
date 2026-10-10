namespace Tsikhanau.Monolith.TestModules.Pings.Features.ProduceError;

internal enum ProducedErrorKind
{
    ValidationWithFields,
    ValidationWithoutField,
    AggregateOfTwoValidations,
    AggregateOfFieldAndFieldlessValidations,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Failure,
    Unexpected,
    AggregateHoldingUnexpected
}
