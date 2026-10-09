using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Database;

namespace Tsikhanau.Monolith.Testing.Database;

public static class ModuleLogins
{
    public static String GetLoginName(IServiceProvider serviceProvider, String schema) =>
        CreateLoginNames(serviceProvider)
            .BuildLoginName(schema);

    public static String GetGroupName(IServiceProvider serviceProvider) => CreateLoginNames(serviceProvider).Group;

    private static ModuleLoginNames CreateLoginNames(IServiceProvider serviceProvider) =>
        new(serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value.LoginPrefix);
}
