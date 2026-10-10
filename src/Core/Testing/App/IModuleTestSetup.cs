using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;

namespace Tsikhanau.Monolith.Testing.App;

public interface IModuleTestSetup
{
    IReadOnlyDictionary<String, String> Settings => ReadOnlyDictionary<String, String>.Empty;

    void ConfigureServices(IServiceCollection services) { }
}
