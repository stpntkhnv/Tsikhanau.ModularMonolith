using System.ComponentModel.DataAnnotations;
using Tsikhanau.Monolith.Application.Settings;

namespace Tsikhanau.Monolith.Infrastructure.Database;

public sealed record DatabaseSettings
{
    public const String Section = $"{SettingsRoot.Section}:Database";

    internal const Int32 MinConnectionsPerModule = 2;

    internal const String LoginPrefixSetting = $"{Section}:{nameof(LoginPrefix)}";

    private const Int32 MinLoginKeyBytes = 32;

    private const String LoginKeySetting = $"{Section}:{nameof(LoginKey)}";

    private const String LoginKeyAdvice =
        "base64 of at least 32 random bytes (openssl rand -base64 32), kept as a secret: "
        + "the AppHost passes its login-key parameter, a server sets App__Database__LoginKey";

    [Required(ErrorMessage = $"{LoginPrefixSetting} is required")]
    [StringLength(30, ErrorMessage = $"{LoginPrefixSetting} must be at most 30 characters long")]
    [RegularExpression(
        ModuleLoginNames.IdentifierPattern,
        ErrorMessage = $"{LoginPrefixSetting} must start with a letter, hold only a-z, 0-9 and _, "
            + "and be neither pg nor start with pg_, which PostgreSQL reserves")]
    public required String LoginPrefix { get; init; }

    [Required(ErrorMessage = $"{LoginKeySetting} is required: {LoginKeyAdvice}")]
    [Base64Bytes(MinLoginKeyBytes, ErrorMessage = $"{LoginKeySetting} must be {LoginKeyAdvice}")]
    public required String LoginKey { get; init; }

    [Range(1, 10_000)]
    public required Int32 MaxConnections { get; init; }

    [Range(typeof(TimeSpan), "00:00:10", "01:00:00")]
    public required TimeSpan PrepareTimeout { get; init; }

    public Int32 CalculatePoolSizePerModule(Int32 moduleCount) =>
        Math.Max(MinConnectionsPerModule, MaxConnections / moduleCount);

    public override String ToString() => nameof(DatabaseSettings);
}
