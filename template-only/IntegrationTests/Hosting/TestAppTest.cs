using Tsikhanau.Monolith.Testing.App;

namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

public abstract class TestAppTest(TestAppFixture testAppFixture) : AppTest<Program, TestAppFixture>(testAppFixture);
