using Tsikhanau.Monolith.Harness.Mutations;
using Tsikhanau.Monolith.Harness.Sources;
using Tsikhanau.Railway;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: Tsikhanau.Monolith.Harness <mutation name>");
    return 2;
}

var name = args[0];

return new MutationCatalog(new SourceTree(Directory.GetCurrentDirectory()))
    .Apply(name)
    .Match(_ => ReportApplied(name), ReportFailure);

static Int32 ReportApplied(String name)
{
    Console.WriteLine($"applied {name}");
    return 0;
}

static Int32 ReportFailure(Error error)
{
    Console.Error.WriteLine($"harness: {error.Message}");
    return 1;
}
