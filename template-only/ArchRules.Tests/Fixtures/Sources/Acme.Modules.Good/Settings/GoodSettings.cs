using System.ComponentModel.DataAnnotations;

namespace Acme.Modules.Good.Settings;

internal sealed record GoodSettings
{
    [Range(1, 100)]
    public required Int32 Limit { get; init; }
}
