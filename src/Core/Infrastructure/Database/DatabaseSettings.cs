using System.ComponentModel.DataAnnotations;
using Tsikhanau.Monolith.Application.Settings;

namespace Tsikhanau.Monolith.Infrastructure.Database;

public sealed record DatabaseSettings
{
    public const String Section = $"{SettingsRoot.Section}:Database";

    internal const String PrepareModeSetting = $"{Section}:{nameof(PrepareMode)}";

    internal const String UpgradeNotes = "the upgrade notes in docs/engineering-principles.md, section 5.6";

    internal const String LoginKeySetting = $"{Section}:{nameof(LoginKey)}";

    internal const String UnknownPrepareMode = $"{PrepareModeSetting} must be OnStartup, Only or Never";

    internal const String RunOnlyFirst =
        $"run the image once with {PrepareModeSetting}=Only and the admin connection string, then start it again";

    internal const Int32 MinConnectionsPerModule = 2;

    private const Int32 MinLoginKeyBytes = 32;

    private const String LoginKeyAdvice =
        "base64 of at least 32 random bytes (openssl rand -base64 32), kept as a secret: "
        + "the AppHost passes its login-key parameter, a server sets App__Database__LoginKey";

    private const String MissingLoginKey =
        $"{LoginKeySetting} is required: {LoginKeyAdvice}; "
        + $"an app that took this core with update-core follows {UpgradeNotes}";


    [Required(ErrorMessage = MissingLoginKey)]
    [Base64Bytes(MinLoginKeyBytes, ErrorMessage = $"{LoginKeySetting} must be {LoginKeyAdvice}")]
    public required String LoginKey { get; init; }

    [EnumDataType(typeof(DatabasePrepareMode), ErrorMessage = UnknownPrepareMode)]
    public DatabasePrepareMode PrepareMode { get; init; } = DatabasePrepareMode.OnStartup;

    [Range(1, 10_000)]
    public required Int32 MaxConnections { get; init; }

    [Range(typeof(TimeSpan), "00:00:10", "01:00:00")]
    public required TimeSpan PrepareTimeout { get; init; }

    public Int32 CalculatePoolSizePerModule(Int32 moduleCount) =>
        Math.Max(MinConnectionsPerModule, MaxConnections / moduleCount);

    public override String ToString() => nameof(DatabaseSettings);
}
