using System.ComponentModel.DataAnnotations;
using Tsikhanau.Monolith.Application.Settings;

namespace Tsikhanau.Monolith.Infrastructure.Database;

public sealed record DatabaseSettings
{
    public const String Section = $"{SettingsRoot.Section}:Database";

    internal const Int32 MinConnectionsPerModule = 2;

    internal const String LoginPrefixSetting = $"{Section}:{nameof(LoginPrefix)}";

    [Required(ErrorMessage = $"{LoginPrefixSetting} is required")]
    [StringLength(30, ErrorMessage = $"{LoginPrefixSetting} must be at most 30 characters long")]
    [RegularExpression(
        ModuleLoginNames.IdentifierPattern,
        ErrorMessage = $"{LoginPrefixSetting} must start with a letter and hold only a-z, 0-9 and _")]
    public required String LoginPrefix { get; init; }

    [Range(1, 10_000)]
    public required Int32 MaxConnections { get; init; }

    [Range(typeof(TimeSpan), "00:00:10", "01:00:00")]
    public required TimeSpan PrepareTimeout { get; init; }

    public Int32 CalculatePoolSizePerModule(Int32 moduleCount) =>
        Math.Max(MinConnectionsPerModule, MaxConnections / moduleCount);
}
