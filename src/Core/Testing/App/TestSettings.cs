namespace Tsikhanau.Monolith.Testing.App;

public static class TestSettings
{
    public const String LoginKey = "dHNpa2hhbmF1LW1vbm9saXRoLXRlc3QtbG9naW5rZXk=";

    public static IReadOnlyDictionary<String, String> CreateEnvironmentVariables(String connectionString) =>
        new Dictionary<String, String>
        {
            ["ConnectionStrings__database"] = connectionString,
            ["App__Database__LoginKey"] = LoginKey,
            ["App__Queues__RunInBackground"] = "false",
            ["App__Cleanup__RunInBackground"] = "false",
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

    private static String BuildEnvironmentVariableName(String key) => key.Replace(":", "__", StringComparison.Ordinal);
}
