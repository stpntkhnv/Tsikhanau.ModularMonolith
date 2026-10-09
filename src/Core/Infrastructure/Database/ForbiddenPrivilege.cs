namespace Tsikhanau.Monolith.Infrastructure.Database;

internal sealed record ForbiddenPrivilege(String Login, String Privilege, String Target)
{
    public String BuildDescription(String group) =>
        $"{Login} has {Privilege} on {Target} (REVOKE {Privilege} ON {Target} FROM {Login}, {group}, PUBLIC)";
}
