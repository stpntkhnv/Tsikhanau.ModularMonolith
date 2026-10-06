using System.Globalization;
using ModularMonolith.Perf.Hosting;

namespace ModularMonolith.Perf.Running;

internal sealed class PerfOptions
{
    private readonly Dictionary<String, String> _values;

    private PerfOptions(IReadOnlyList<String> arguments, Dictionary<String, String> values)
    {
        Arguments = arguments;
        _values = values;
    }

    public IReadOnlyList<String> Arguments { get; }

    public static PerfOptions Parse(IEnumerable<String> arguments)
    {
        var argumentList = arguments.ToList();
        var values = argumentList
            .Select(argument => argument.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1]);

        return new PerfOptions(argumentList, values);
    }

    public String GetString(String key, String fallback) => _values.GetValueOrDefault(key, fallback);

    public Int32 GetInt32(String key, Int32 fallback) =>
        _values.TryGetValue(key, out var value) ? ParseInt32(value) : fallback;

    public IReadOnlyList<Int32> GetInt32List(String key, String fallback) =>
        GetString(key, fallback)
            .Split(',')
            .Select(ParseInt32)
            .ToList();

    public PerfHostSettings GetHostSettings(Boolean runInBackground) =>
        new(
            runInBackground,
            GetInt32("maxconn", 60),
            TimeSpan.FromMilliseconds(GetInt32("pollms", 5000)),
            GetString("logging", "none") == "console");

    private static Int32 ParseInt32(String value) => Int32.Parse(value, CultureInfo.InvariantCulture);
}
