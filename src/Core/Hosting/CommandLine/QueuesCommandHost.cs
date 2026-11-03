using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Tsikhanau.Monolith.Infrastructure.Modules;
using Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

namespace Tsikhanau.Monolith.Hosting.CommandLine;

internal static class QueuesCommandHost
{
    public static async Task<Int32> HostQueuesToolAsync(String[] arguments, Action<WebApplicationBuilder> addServices)
    {
        try
        {
            await using var app = BuildApp(addServices);
            _ = app.Services.ValidateSettings();
            await using var outputTextWriter = StandardOutput.CreateWriter();
            var queuesTool = new QueuesTool(app.Services, outputTextWriter, Console.Error);
            return await queuesTool.RunAsync(arguments, CancellationToken.None);
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"{QueuesTool.Name}: {exception.Message}");
            return QueuesTool.FailedExitCode;
        }
    }

    private static WebApplication BuildApp(Action<WebApplicationBuilder> addServices)
    {
        var builder = MonolithHost.CreateBuilder([], addServices);
        builder.Logging
            .AddConsole(WriteEveryLevelToStandardError)
            .AddFilter<ConsoleLoggerProvider>(category: null, LogLevel.Warning);

        return builder.Build();
    }

    private static void WriteEveryLevelToStandardError(ConsoleLoggerOptions consoleLoggerOptions) =>
        consoleLoggerOptions.LogToStandardErrorThreshold = LogLevel.Trace;
}
