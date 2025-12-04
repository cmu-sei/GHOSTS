var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("ghosts-postgres")
    .WithPgAdmin(pgAdmin =>
    {
        pgAdmin.WithLifetime(ContainerLifetime.Persistent);
    });

var db = postgres.AddDatabase("ghosts", "ghosts");

var api = builder.AddProject<Projects.Ghosts.Api>("api")
    .WaitFor(postgres)
    .WithHttpHealthCheck("api")
    .WithReference(db, "DefaultConnection")
    .WithEnvironment("ConnectionStrings__Provider", "PostgreSQL");

builder.Build().Run();
