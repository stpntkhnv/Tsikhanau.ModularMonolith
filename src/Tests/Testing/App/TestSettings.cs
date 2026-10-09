using Tsikhanau.Monolith.Infrastructure.Platform;

namespace ModularMonolith.Testing.App;

public static class TestSettings
{
    public static IReadOnlyDictionary<String, String> CreateEnvironmentVariables(String connectionString) =>
        new Dictionary<String, String>
        {
            [$"ConnectionStrings__{PlatformServiceCollectionExtensions.ConnectionName}"] = connectionString,
            ["ModularMonolith__Queues__RunInBackground"] = "false",
            ["ModularMonolith__Cleanup__RunInBackground"] = "false",
        };
}
