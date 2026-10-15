using System.ComponentModel.DataAnnotations;

namespace Tsikhanau.Monolith.Infrastructure.Database;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class Base64BytesAttribute(Int32 minimumCount) : ValidationAttribute
{
    public Int32 MinimumCount { get; } = minimumCount;

    public override Boolean IsValid(Object? value) => value is not String text || DecodesToEnoughBytes(text);

    private Boolean DecodesToEnoughBytes(String text) =>
        Convert.TryFromBase64String(text, new Byte[text.Length], out var byteCount) && byteCount >= MinimumCount;
}
