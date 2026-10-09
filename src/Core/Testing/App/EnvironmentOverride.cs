namespace Tsikhanau.Monolith.Testing.App;

public sealed class EnvironmentOverride : IDisposable
{
    private readonly Dictionary<String, String?> _previousValues;

    public EnvironmentOverride(IReadOnlyDictionary<String, String> values)
    {
        _previousValues = values.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        foreach (var (key, value) in values)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public void Dispose()
    {
        foreach (var (key, value) in _previousValues)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
