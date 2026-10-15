using System.Security.Cryptography;
using Aspire.Hosting.Publishing;

namespace ModularMonolith.AppHost;

internal sealed class LoginKeyDefault : ParameterDefault
{
    private const Int32 KeyByteCount = 32;

    private const Int32 Base64LengthOf33Bytes = 44;

    private static readonly GenerateParameterDefault LettersAndDigitsOfBase64Length = new()
    {
        MinLength = Base64LengthOf33Bytes,
        Special = false,
    };

    public override void WriteToManifest(ManifestPublishingContext context) =>
        LettersAndDigitsOfBase64Length.WriteToManifest(context);

    public override String GetDefaultValue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyByteCount));
}
