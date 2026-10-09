using Tsikhanau.Monolith.Testing.App;

namespace ModularMonolith.Testing.App;

public sealed class AppFactory(TimeProvider timeProvider, String environmentName)
    : AppFactory<Program>(timeProvider, environmentName);
