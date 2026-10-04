using Xunit;

namespace ModularMonolith.Testing.App;

public abstract class AppTest(AppFixture appFixture) : IAsyncLifetime
{
    protected AppFixture App { get; } = appFixture;

    public ValueTask InitializeAsync() => new(App.ResetAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
