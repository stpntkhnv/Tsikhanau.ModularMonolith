using Tsikhanau.Railway;

namespace Tsikhanau.Monolith.Perf.Scenarios;

internal static class ResultExtensions
{
    extension<T>(Result<T> result)
        where T : notnull
    {
        public T GetValueOrThrow() =>
            result.IsSuccess ? result.Value : throw new InvalidOperationException(result.Error.ToString());
    }
}
