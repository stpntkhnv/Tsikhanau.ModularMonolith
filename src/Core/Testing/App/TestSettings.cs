using Tsikhanau.Monolith.Infrastructure.Cleanup;
using Tsikhanau.Monolith.Infrastructure.Database.Settings;
using Tsikhanau.Monolith.Infrastructure.Queues;

namespace Tsikhanau.Monolith.Testing.App;

public static class TestSettings
{
    public const String LoginKey = "dHNpa2hhbmF1LW1vbm9saXRoLXRlc3QtbG9naW5rZXk=";

    internal const String QueuesInBackgroundSetting =
        $"{QueueSettings.Section}:{nameof(QueueSettings.RunInBackground)}";

    internal const String CleanupInBackgroundSetting =
        $"{CleanupSettings.Section}:{nameof(CleanupSettings.RunInBackground)}";

    internal static String ConnectionStringVariable { get; } =
        BuildEnvironmentVariableName(DatabaseConnectionStrings.Setting);

    public static IReadOnlyDictionary<String, String> CreateEnvironmentVariables(String connectionString) =>
        new Dictionary<String, String>
        {
            [ConnectionStringVariable] = connectionString,
            [BuildEnvironmentVariableName(DatabaseSettings.LoginKeySetting)] = LoginKey,
            [BuildEnvironmentVariableName(QueuesInBackgroundSetting)] = "false",
            [BuildEnvironmentVariableName(CleanupInBackgroundSetting)] = "false",
        };

    internal static IReadOnlyDictionary<String, String> CreateEnvironmentVariables(
        String connectionString,
        IEnumerable<KeyValuePair<String, String>> moduleSettings,
        IReadOnlyDictionary<String, String> overrides)
    {
        var environmentVariables = new Dictionary<String, String>(CreateEnvironmentVariables(connectionString));
        foreach (var (key, value) in moduleSettings.Concat(overrides))
        {
            environmentVariables[BuildEnvironmentVariableName(key)] = value;
        }

        return environmentVariables;
    }

    internal static String BuildEnvironmentVariableName(String key) => key.Replace(":", "__", StringComparison.Ordinal);
}
