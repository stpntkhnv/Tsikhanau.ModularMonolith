using ModularMonolith.Testing.App;
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.IntegrationTests.Modules;

public sealed class ModuleDatabaseAccessTests(AppFixture appFixture)
    : ModuleDatabaseAccessTests<Program, AppFixture>(appFixture);
