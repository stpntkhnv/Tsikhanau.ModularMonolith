using Tsikhanau.Monolith.Testing.App;

namespace ModularMonolith.Testing.App;

public abstract class AppTest(AppFixture appFixture) : AppTest<Program, AppFixture>(appFixture);
