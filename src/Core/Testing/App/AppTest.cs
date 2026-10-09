using Xunit;

namespace Tsikhanau.Monolith.Testing.App;

public abstract class AppTest<TEntryPoint, TAppFixture>(TAppFixture appFixture) : IAsyncLifetime
    where TEntryPoint : class
    where TAppFixture : AppFixture<TEntryPoint>
{
    protected TAppFixture App { get; } = appFixture;

    public ValueTask InitializeAsync() => new(App.ResetAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
