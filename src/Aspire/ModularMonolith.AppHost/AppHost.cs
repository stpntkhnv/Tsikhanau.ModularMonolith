var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume();

var appDatabase = postgres.AddDatabase("database", databaseName: "modularmonolith");

builder
    .AddProject<Projects.Bootstrapper>("api")
    .WithReference(appDatabase)
    .WaitFor(appDatabase);

await builder
    .Build()
    .RunAsync();
