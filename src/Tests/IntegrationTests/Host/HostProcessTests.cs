using ModularMonolith.Testing.App;
using Tsikhanau.Monolith.Testing.Host;

namespace ModularMonolith.IntegrationTests.Host;

public sealed class HostProcessTests(AppFixture appFixture) : HostProcessTests<Program, AppFixture>(appFixture);
