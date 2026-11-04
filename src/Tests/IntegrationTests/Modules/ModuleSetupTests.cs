using ModularMonolith.Testing.App;
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.IntegrationTests.Modules;

public sealed class ModuleSetupTests(AppFixture appFixture) : ModuleSetupTests<Program, AppFixture>(appFixture);
