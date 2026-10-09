using Tsikhanau.Monolith.IntegrationTests.Hosting;
using Xunit.Sdk;
using Xunit.v3;

[assembly: AssemblyFixture(typeof(PostgresFixture))]
[assembly: Parallelization(Mode = ParallelMode.None)]
