using Acme.Modules.Bad.Http;

namespace Acme.Modules.Bad.Infrastructure.Routing;

internal static class RouteBinder
{
    public static String Bind(CrateRoutes crateRoutes) => crateRoutes.Prefix;
}
