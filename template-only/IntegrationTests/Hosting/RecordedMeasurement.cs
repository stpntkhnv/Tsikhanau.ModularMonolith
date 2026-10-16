namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

internal sealed record RecordedMeasurement(String Instrument, Double Value, IReadOnlyDictionary<String, String> Tags);
