using ModularMonolith.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume();

var appDatabase = postgres.AddDatabase("database", databaseName: "modularmonolith");

var loginKey = builder.AddParameter("login-key", new LoginKeyDefault(), secret: true, persist: true);

builder
    .AddProject<Projects.Bootstrapper>("api")
    .WithReference(appDatabase)
    .WithEnvironment("App__Database__LoginKey", loginKey)
    .WaitFor(appDatabase);

await builder
    .Build()
    .RunAsync();
