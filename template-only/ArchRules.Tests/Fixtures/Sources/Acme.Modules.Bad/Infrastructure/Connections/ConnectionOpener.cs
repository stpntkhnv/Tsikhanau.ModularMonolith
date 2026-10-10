using Npgsql;

namespace Acme.Modules.Bad.Infrastructure.Connections;

internal static class ConnectionOpener
{
    public static NpgsqlConnection Open(String connectionString) => new(connectionString);
}
