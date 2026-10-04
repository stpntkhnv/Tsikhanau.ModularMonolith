using ModularMonolith.Testing.App;
using Xunit.Sdk;
using Xunit.v3;

[assembly: AssemblyFixture(typeof(AppFixture))]
[assembly: Parallelization(Mode = ParallelMode.None)]
