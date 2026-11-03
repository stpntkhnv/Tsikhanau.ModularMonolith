namespace Tsikhanau.Monolith.IntegrationTests.Hosts.TestApp;

[CollectionDefinition(Name)]
public sealed class TestAppCollection : ICollectionFixture<TestAppFixture>
{
    public const String Name = "test app";
}
