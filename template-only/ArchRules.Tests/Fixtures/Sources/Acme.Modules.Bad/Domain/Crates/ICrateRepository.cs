namespace Acme.Modules.Bad.Domain.Crates;

internal interface ICrateRepository
{
    String Describe(Guid crateId);
}
