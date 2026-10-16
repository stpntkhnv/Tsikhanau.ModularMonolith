using Npgsql;
using Tsikhanau.Monolith.Infrastructure.Modules;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal static class ModuleConnection
{
    public static async Task<NpgsqlConnection> OpenAsync(
        ModuleDescriptor module,
        NpgsqlDataSource moduleNpgsqlDataSource,
        CancellationToken cancellationToken)
    {
        try
        {
            return await moduleNpgsqlDataSource.OpenConnectionAsync(cancellationToken);
        }
        catch (PostgresException exception) when (IsRefused(exception))
        {
            throw new InvalidOperationException(
                $"Module {module.Name} cannot sign in as {ReadLoginName(moduleNpgsqlDataSource)} "
                + $"({exception.MessageText}): the database is not prepared for this app "
                + $"and its {DatabaseSettings.LoginKeySetting}; {DatabaseSettings.RunOnlyFirst}",
                exception);
        }
    }

    private static Boolean IsRefused(PostgresException exception) =>
        exception.SqlState is PostgresErrorCodes.InvalidPassword
            or PostgresErrorCodes.InvalidAuthorizationSpecification;

    private static String ReadLoginName(NpgsqlDataSource npgsqlDataSource) =>
        new NpgsqlConnectionStringBuilder(npgsqlDataSource.ConnectionString).Username ?? String.Empty;
}
