using ModularMonolith.Testing.App;
using Tsikhanau.Monolith.Testing.Host;

namespace ModularMonolith.IntegrationTests.Host;

public sealed class HostTests(AppFixture appFixture) : HostTests<Program, AppFixture>(appFixture);
