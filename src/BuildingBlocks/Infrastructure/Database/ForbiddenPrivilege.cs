namespace ModularMonolith.BuildingBlocks.Infrastructure.Database;

internal sealed record ForbiddenPrivilege(String Login, String Privilege, String Target)
{
    public override String ToString() =>
        $"{Login} has {Privilege} on {Target} "
        + $"(REVOKE {Privilege} ON {Target} FROM {Login}, {ModuleLogin.Group}, PUBLIC)";
}
