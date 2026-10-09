using System.Globalization;
using Tsikhanau.Monolith.Perf.Running;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal sealed class AutovacuumParameters
{
    private static readonly IReadOnlyDictionary<String, String> ParameterNames = new Dictionary<String, String>
    {
        ["scale"] = "autovacuum_vacuum_scale_factor",
        ["threshold"] = "autovacuum_vacuum_threshold",
        ["insertscale"] = "autovacuum_vacuum_insert_scale_factor",
        ["insertthreshold"] = "autovacuum_vacuum_insert_threshold",
        ["costlimit"] = "autovacuum_vacuum_cost_limit",
        ["costdelay"] = "autovacuum_vacuum_cost_delay",
    };

    private readonly IReadOnlyList<(String Name, Double Value)> _values;

    private AutovacuumParameters(IReadOnlyList<(String Name, Double Value)> values) => _values = values;

    public Boolean KeepTableSettings => _values.Count == 0;

    public String Description =>
        KeepTableSettings
            ? "table settings from the migrations"
            : String.Join(", ", _values.Select(value => $"{value.Name} = {FormatValue(value.Value)}"));

    public static AutovacuumParameters From(PerfOptions options) =>
        new(
            ParameterNames
                .Where(parameter => options.GetString(parameter.Key, "").Length > 0)
                .Select(parameter => (parameter.Value, ParseValue(options.GetString(parameter.Key, ""))))
                .ToList());

    public String BuildSetSql(QueueTable queueTable) =>
        $"ALTER TABLE \"{queueTable.Schema}\".{queueTable.Table} SET ({Description})";

    public String BuildResetSql(QueueTable queueTable) =>
        $"ALTER TABLE \"{queueTable.Schema}\".{queueTable.Table} RESET ({BuildNames()})";

    private String BuildNames() =>
        String.Join(", ", _values.Select(value => value.Name));

    private static Double ParseValue(String value) => Double.Parse(value, CultureInfo.InvariantCulture);

    private static String FormatValue(Double value) => value.ToString(CultureInfo.InvariantCulture);
}
