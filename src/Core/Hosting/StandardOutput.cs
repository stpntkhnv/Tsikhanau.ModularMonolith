using Microsoft.Win32.SafeHandles;

namespace Tsikhanau.Monolith.Hosting;

internal static class StandardOutput
{
    private const Int32 UnixFileDescriptor = 1;

    public static StreamWriter CreateWriter() => new(Open());

    private static Stream Open()
    {
        if (OperatingSystem.IsWindows())
        {
            return Console.OpenStandardOutput();
        }

        var fileStream = new FileStream(
            new SafeFileHandle(UnixFileDescriptor, ownsHandle: false),
            FileAccess.Write,
            bufferSize: 0);

        if (!fileStream.CanSeek)
        {
            return fileStream;
        }

        fileStream.Dispose();
        return Console.OpenStandardOutput();
    }
}
