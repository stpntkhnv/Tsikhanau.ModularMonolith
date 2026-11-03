using Tsikhanau.Monolith.Hosting;

return await MonolithHost.RunAsync(args, AddServices);

static void AddServices(WebApplicationBuilder builder)
{
}

public partial class Program;
