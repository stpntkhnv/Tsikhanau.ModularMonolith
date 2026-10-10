using System.Runtime.InteropServices;
using Tsikhanau.Monolith.Scaffold.Cli;
using Tsikhanau.Monolith.Scaffold.Files;
using Tsikhanau.Monolith.Scaffold.Transactions;

using var cancellation = new CancellationTokenSource();
using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context => Stop(context, cancellation));
using var termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => Stop(context, cancellation));

var root = Directory.GetCurrentDirectory();
var tool = new ScaffoldTool(
    new PhysicalFileSystem(root),
    new ProcessRunner(root, Console.Out),
    Console.Out,
    Console.Error);

return await tool.RunAsync(args, cancellation.Token);

static void Stop(PosixSignalContext context, CancellationTokenSource cancellation)
{
    context.Cancel = true;
    cancellation.Cancel();
}
