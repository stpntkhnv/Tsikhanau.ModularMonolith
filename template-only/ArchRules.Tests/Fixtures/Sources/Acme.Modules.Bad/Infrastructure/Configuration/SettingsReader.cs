using Microsoft.Extensions.Configuration;

namespace Acme.Modules.Bad.Infrastructure.Configuration;

internal sealed class SettingsReader(IConfiguration configuration)
{
    public String? Read(String key) => configuration[key];
}
