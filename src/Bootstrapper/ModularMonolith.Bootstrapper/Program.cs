using ModularMonolith.Bootstrapper.Defaults;
using ModularMonolith.Bootstrapper.Platform;
using ModularMonolith.BuildingBlocks.Api.Modules;
using ModularMonolith.BuildingBlocks.Infrastructure.Diagnostics;
using ModularMonolith.BuildingBlocks.Infrastructure.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(AppDiagnostics.SourceName);
builder.AddPlatform();

var app = builder.Build();

await app.Services.PrepareModuleDatabasesAsync();
app.UseServiceDefaults();
app.UseModules();
app.MapModules();

await app.RunAsync();

public partial class Program;
