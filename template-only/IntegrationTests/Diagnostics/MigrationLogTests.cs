using Microsoft.Extensions.Logging;
using ModularMonolith.Testing.Database;
using Tsikhanau.Monolith.IntegrationTests.Hosting;

namespace Tsikhanau.Monolith.IntegrationTests.Diagnostics;

public sealed class MigrationLogTests(PostgresFixture postgresFixture)
{
    private const String MigrationsCategory = "Microsoft.EntityFrameworkCore.Migrations";

    private const String UpToDate = "No migrations were applied. The database is already up to date.";

    [Fact]
    public async Task Database_preparation_logs_the_migration_outcome_of_every_module_at_information()
    {
        var capturingLoggerProvider = new CapturingLoggerProvider();
        await using var earlierHost = await TestModulesHost.StartAsync(postgresFixture);
        await using var testModulesHost = await TestModulesHost.StartAsync(
            postgresFixture,
            configure: builder => HostLoggingSettings
                .ApplyTo(builder.Logging)
                .AddProvider(capturingLoggerProvider));

        var migrationEntries = capturingLoggerProvider.Entries
            .Where(entry => entry.Category == MigrationsCategory)
            .Select(entry => (entry.Level, entry.Message))
            .ToList();

        migrationEntries.ShouldBe([(LogLevel.Information, UpToDate), (LogLevel.Information, UpToDate)]);
    }
}
