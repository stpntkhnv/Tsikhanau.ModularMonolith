using ModularMonolith.Testing.App;
using Tsikhanau.Monolith.Testing.Host;

namespace ModularMonolith.IntegrationTests.Host;

public sealed class TelemetrySourceTests(AppFixture appFixture) : TelemetrySourceTests<Program, AppFixture>(appFixture);
