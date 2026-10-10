using System.Diagnostics;

namespace Tsikhanau.Monolith.Scaffold.Tests.Support;

internal static class ProcessProbe
{
    private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<Int32> ReadProcessIdAsync(String path)
    {
        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt) < PatienceLimit)
        {
            var text = File.Exists(path) ? await File.ReadAllTextAsync(path, CancellationToken) : String.Empty;
            if (text.EndsWith('\n'))
            {
                return Int32.Parse(text, provider: null);
            }

            await Task.Delay(PollInterval, CancellationToken);
        }

        throw new TimeoutException($"no process wrote {path} in {PatienceLimit}");
    }

    public static async Task<Int32> ReadChildIdAsync(Int32 parentId)
    {
        var path = $"/proc/{parentId}/task/{parentId}/children";
        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt) < PatienceLimit)
        {
            var children = File.Exists(path) ? await File.ReadAllTextAsync(path, CancellationToken) : String.Empty;
            if (children.Trim().Length > 0)
            {
                return Int32.Parse(children.Split(' ')[0], provider: null);
            }

            await Task.Delay(PollInterval, CancellationToken);
        }

        throw new TimeoutException($"process {parentId} started no child in {PatienceLimit}");
    }

    public static async Task<Boolean> WaitWhileRunningAsync(Int32 processId)
    {
        var startedAt = Stopwatch.GetTimestamp();
        while (IsRunning(processId) && Stopwatch.GetElapsedTime(startedAt) < PatienceLimit)
        {
            await Task.Delay(PollInterval, CancellationToken);
        }

        return IsRunning(processId);
    }

    public static void KillIfRunning(Int32 processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill();
        }
        catch (ArgumentException)
        {
        }
    }

    public static Boolean IsRunning(Int32 processId)
    {
        try
        {
            var status = File.ReadAllText($"/proc/{processId}/stat");
            return status[status.LastIndexOf(')') + 2] != 'Z';
        }
        catch (IOException)
        {
            return false;
        }
    }
}
