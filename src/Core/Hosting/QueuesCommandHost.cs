using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Tsikhanau.Monolith.Infrastructure.Queues.CommandLine;

namespace Tsikhanau.Monolith.Hosting;

internal static class QueuesCommandHost
{
    public static async Task<Int32> RunAsync(String[] arguments, Action<WebApplicationBuilder> addServices)
    {
        try
        {
            await using var app = Build(addServices);
            ValidateSettings(app.Services);
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

    private static WebApplication Build(Action<WebApplicationBuilder> addServices)
    {
        var builder = WebApplication.CreateBuilder([]);
        addServices(builder);
        builder.Logging
            .AddConsole(WriteEveryLevelToStandardError)
            .AddFilter<ConsoleLoggerProvider>(category: null, LogLevel.Warning);

        return builder.Build();
    }

    private static void WriteEveryLevelToStandardError(ConsoleLoggerOptions consoleLoggerOptions) =>
        consoleLoggerOptions.LogToStandardErrorThreshold = LogLevel.Trace;

    private static void ValidateSettings(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<IStartupValidator>()
            .Validate();
}
