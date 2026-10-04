using ModularMonolith.BuildingBlocks.Infrastructure.Database;

namespace ModularMonolith.BuildingBlocks.UnitTests.Database;

public sealed class PostgresVersionTests
{
    [Theory]
    [InlineData(170000)]
    [InlineData(170006)]
    [InlineData(180000)]
    public void PostgreSQL_17_or_later_is_supported(Int32 serverVersionNumber) =>
        Should.NotThrow(() => PostgresVersion.EnsureSupported(serverVersionNumber));

    [Fact]
    public void Server_older_than_PostgreSQL_17_is_refused_with_its_version()
    {
        var exception = Should.Throw<InvalidOperationException>(() => PostgresVersion.EnsureSupported(169999));

        exception.Message.ShouldContain("169999");
        exception.Message.ShouldContain("ModularMonolith needs PostgreSQL 17 or later");
    }
}
