var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume();

var appDatabase = postgres.AddDatabase("modularmonolith");

builder
    .AddProject<Projects.ModularMonolith_Bootstrapper>("api")
    .WithReference(appDatabase)
    .WaitFor(appDatabase);

await builder
    .Build()
    .RunAsync();
