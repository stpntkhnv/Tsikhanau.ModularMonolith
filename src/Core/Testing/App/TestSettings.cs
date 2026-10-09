namespace Tsikhanau.Monolith.Testing.App;

public static class TestSettings
{
    public static IReadOnlyDictionary<String, String> CreateEnvironmentVariables(String connectionString) =>
        new Dictionary<String, String>
        {
            ["ConnectionStrings__database"] = connectionString,
            ["App__Queues__RunInBackground"] = "false",
            ["App__Cleanup__RunInBackground"] = "false",
        };
}
