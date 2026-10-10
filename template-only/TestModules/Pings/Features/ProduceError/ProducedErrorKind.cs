namespace Tsikhanau.Monolith.TestModules.Pings.Features.ProduceError;

internal enum ProducedErrorKind
{
    ValidationWithFields,
    ValidationWithoutField,
    AggregateOfTwoValidations,
    AggregateOfFieldAndFieldlessValidations,
    ValidationWithContext,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Failure,
    Unexpected,
    AggregateHoldingUnexpected
}
