using Tsikhanau.Monolith.IntegrationTests.Hosts.TestApp;
using Tsikhanau.Monolith.Testing.Host;

namespace Tsikhanau.Monolith.IntegrationTests.App;

[Collection(TestAppCollection.Name)]
public sealed class HostProcessTests(TestAppFixture testAppFixture)
    : HostProcessTests<Program, TestAppFixture>(testAppFixture);
