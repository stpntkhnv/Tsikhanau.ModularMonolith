using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.Hosts.TestApp;

public abstract class TestAppTest(TestAppFixture testAppFixture) : AppTest<Program, TestAppFixture>(testAppFixture);
