namespace Tsikhanau.Monolith.TestHost.Probes;

internal sealed class SecretHandlerProbe(TimeSpan blockDuration, String effectPath)
{
    public const String BlocksMarker = "probe: secret handler blocks";

    public const String EndsMarker = "probe: secret handler ends";

    private const String EffectLine = "effect\n";

    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        await Console.Out.WriteLineAsync(BlocksMarker);
        try
        {
            await Task.Delay(blockDuration, cancellationToken);
        }
        finally
        {
            await Console.Out.WriteLineAsync(EndsMarker);
        }

        await File.AppendAllTextAsync(effectPath, EffectLine, cancellationToken);
    }
}
