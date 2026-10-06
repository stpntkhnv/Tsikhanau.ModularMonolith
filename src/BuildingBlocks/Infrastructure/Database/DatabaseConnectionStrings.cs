using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ModularMonolith.BuildingBlocks.Infrastructure.Database;

internal sealed class DatabaseConnectionStrings
{
    public const String Name = "modularmonolith";

    private const Int32 MinCommandTimeoutSeconds = 30;

    private const Int32 MaxAutoPreparedStatements = 20;

    private readonly NpgsqlConnectionStringBuilder _adminConnectionStringBuilder;

    private DatabaseConnectionStrings(NpgsqlConnectionStringBuilder adminConnectionStringBuilder) =>
        _adminConnectionStringBuilder = adminConnectionStringBuilder;

    public String Admin =>
        new NpgsqlConnectionStringBuilder(_adminConnectionStringBuilder.ConnectionString)
        {
            Pooling = false,
        }.ConnectionString;

    public static DatabaseConnectionStrings From(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(Name);
        if (String.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{Name}' is missing");
        }

        var adminConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            PersistSecurityInfo = false,
        };

        if (String.IsNullOrEmpty(adminConnectionStringBuilder.Password))
        {
            throw new InvalidOperationException(
                $"Connection string '{Name}' has no password; module logins derive their passwords from it");
        }

        return new DatabaseConnectionStrings(adminConnectionStringBuilder);
    }

    public ModuleLogin CreateModuleLogin(String schema) =>
        ModuleLogin.Create(schema, _adminConnectionStringBuilder.Password!);

    public String BuildForModule(
        ModuleLogin login,
        String applicationName,
        Int32 maxPoolSize,
        TimeSpan handlerTimeout) =>
        new NpgsqlConnectionStringBuilder(_adminConnectionStringBuilder.ConnectionString)
        {
            Username = login.Name,
            Password = login.Password,
            ApplicationName = applicationName,
            Pooling = true,
            MaxPoolSize = maxPoolSize,
            CommandTimeout = CalculateCommandTimeout(handlerTimeout),
            MaxAutoPrepare = MaxAutoPreparedStatements,
        }.ConnectionString;

    private static Int32 CalculateCommandTimeout(TimeSpan handlerTimeout) =>
        Math.Max(MinCommandTimeoutSeconds, (Int32)Math.Ceiling(handlerTimeout.TotalSeconds));
}
