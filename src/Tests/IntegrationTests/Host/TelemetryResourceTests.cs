using ModularMonolith.Testing.App;
using Tsikhanau.Monolith.Testing.Host;

namespace ModularMonolith.IntegrationTests.Host;

public sealed class TelemetryResourceTests(AppFixture appFixture)
    : TelemetryResourceTests<Program, AppFixture>(appFixture);
