namespace Tsikhanau.Monolith.IntegrationTests.Telemetry;

internal sealed record RecordedMeasurement(String Instrument, Double Value, IReadOnlyDictionary<String, String> Tags);
