namespace Tsikhanau.Monolith.IntegrationTests.Hosting;

[CollectionDefinition(Name)]
public sealed class TestAppCollection : ICollectionFixture<TestAppFixture>
{
    public const String Name = "test app";
}
