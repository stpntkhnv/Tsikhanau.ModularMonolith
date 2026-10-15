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

    private readonly NpgsqlConnectionStringBuilder _adminConnectionStringBuilder;

    private DatabaseConnectionStrings(NpgsqlConnectionStringBuilder adminConnectionStringBuilder) =>
        _adminConnectionStringBuilder = adminConnectionStringBuilder;

    public static DatabaseConnectionStrings From(IConfiguration configuration, IHostEnvironment hostEnvironment)
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
                $"Connection string '{Name}' has no password; database preparation signs in with it");
        }

        EnsureSslModeIsExplicit(adminConnectionStringBuilder.SslMode, hostEnvironment);
        return new DatabaseConnectionStrings(adminConnectionStringBuilder);
    }

    public String BuildForAdmin(TimeSpan prepareTimeout) =>
        new NpgsqlConnectionStringBuilder(_adminConnectionStringBuilder.ConnectionString)
        {
            Pooling = false,
            CommandTimeout = prepareTimeout.TotalSecondsRoundedUp,
        }.ConnectionString;

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
