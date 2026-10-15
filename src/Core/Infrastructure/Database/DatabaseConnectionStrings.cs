using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Tsikhanau.Monolith.Shared.Time;

namespace Tsikhanau.Monolith.Infrastructure.Database;

internal sealed class DatabaseConnectionStrings
{
    public const String Name = "database";

    private const Int32 MinCommandTimeoutSeconds = 30;

    private const Int32 MaxAutoPreparedStatements = 20;

    private const String SslModeRequirement =
        "must set SSL Mode to VerifyFull, VerifyCA, Require or Disable outside Development";

    private const String MissingAdminPassword =
        $"Connection string '{Name}' has no password; {DatabaseSettings.PrepareModeSetting} OnStartup (the default) "
        + "and Only sign in with it to prepare the database";

    private const String PasswordWithoutPreparation =
        $"Connection string '{Name}' holds a password, but {DatabaseSettings.PrepareModeSetting} is Never, "
        + "which does no admin work: give it only the server, the Database and the SSL Mode; "
        + $"the module logins sign in with passwords derived from {DatabaseSettings.LoginKeySetting}";

    private readonly NpgsqlConnectionStringBuilder _configuredConnectionStringBuilder;

    private DatabaseConnectionStrings(
        NpgsqlConnectionStringBuilder configuredConnectionStringBuilder,
        String databaseName)
    {
        _configuredConnectionStringBuilder = configuredConnectionStringBuilder;
        DatabaseName = databaseName;
    }

    public String DatabaseName { get; }

    public static DatabaseConnectionStrings From(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        var connectionString = configuration.GetConnectionString(Name);
        if (String.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{Name}' is missing");
        }

        var configuredConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            PersistSecurityInfo = false,
        };

        var databaseName = configuredConnectionStringBuilder.Database;
        if (String.IsNullOrEmpty(databaseName))
        {
            throw new InvalidOperationException(
                $"Connection string '{Name}' names no Database; the module roles are named after the database");
        }

        EnsurePasswordFitsThePrepareMode(configuredConnectionStringBuilder.Password, ReadPrepareMode(configuration));
        EnsureSslModeIsExplicit(configuredConnectionStringBuilder.SslMode, hostEnvironment);
        return new DatabaseConnectionStrings(configuredConnectionStringBuilder, databaseName);
    }

    public String BuildForAdmin(TimeSpan prepareTimeout) =>
        new NpgsqlConnectionStringBuilder(_configuredConnectionStringBuilder.ConnectionString)
        {
            Pooling = false,
            CommandTimeout = prepareTimeout.TotalSecondsRoundedUp,
        }.ConnectionString;

    public String BuildForModule(
        ModuleLogin login,
        String applicationName,
        Int32 maxPoolSize,
        TimeSpan handlerTimeout) =>
        new NpgsqlConnectionStringBuilder(_configuredConnectionStringBuilder.ConnectionString)
        {
            Username = login.Name,
            Password = login.Password,
            ApplicationName = applicationName,
            Pooling = true,
            MaxPoolSize = maxPoolSize,
            CommandTimeout = CalculateCommandTimeout(handlerTimeout),
            MaxAutoPrepare = MaxAutoPreparedStatements,
        }.ConnectionString;

    private static DatabasePrepareMode ReadPrepareMode(IConfiguration configuration)
    {
        var prepareMode = configuration.GetValue(DatabaseSettings.PrepareModeSetting, DatabasePrepareMode.OnStartup);
        return Enum.IsDefined(prepareMode)
            ? prepareMode
            : throw new InvalidOperationException(DatabaseSettings.UnknownPrepareMode);
    }

    private static void EnsurePasswordFitsThePrepareMode(String? password, DatabasePrepareMode prepareMode)
    {
        var hasPassword = !String.IsNullOrEmpty(password);
        if (prepareMode == DatabasePrepareMode.Never && hasPassword)
        {
            throw new InvalidOperationException(PasswordWithoutPreparation);
        }

        if (prepareMode != DatabasePrepareMode.Never && !hasPassword)
        {
            throw new InvalidOperationException(MissingAdminPassword);
        }
    }

    private static void EnsureSslModeIsExplicit(SslMode sslMode, IHostEnvironment hostEnvironment)
    {
        if (!hostEnvironment.IsDevelopment() && !IsExplicitChoice(sslMode))
        {
            throw new InvalidOperationException($"Connection string '{Name}' {SslModeRequirement}, not {sslMode}");
        }
    }

    private static Boolean IsExplicitChoice(SslMode sslMode) =>
        sslMode is SslMode.VerifyFull or SslMode.VerifyCA or SslMode.Require or SslMode.Disable;

    private static Int32 CalculateCommandTimeout(TimeSpan handlerTimeout) =>
        Math.Max(MinCommandTimeoutSeconds, handlerTimeout.TotalSecondsRoundedUp);
}
