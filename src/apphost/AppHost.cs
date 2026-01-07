var builder = DistributedApplication.CreateBuilder(args);

var postgresUsername = builder.AddParameter("PostgresUsername", "postgres", false);
var postgresPassword = builder.AddParameter("PostgresPassword", "postgres", false);

var postgres = builder.AddPostgres("postgres", userName: postgresUsername, password: postgresPassword)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("ghosts-postgres")
    .WithPgAdmin(pgAdmin =>
    {
        pgAdmin.WithEndpoint("http", endpoint => endpoint.Port = 33000);
    });

var db = postgres.AddDatabase("ghosts", "ghosts");

var api = builder.AddProject<Projects.Ghosts_Api>("api")
    .WaitFor(postgres)
    .WithReference(db, "DefaultConnection")
    .WithEnvironment("ConnectionStrings__Provider", "PostgreSQL");

var frontend = builder.AddJavaScriptApp("frontend", "../ghosts.ng", "start")
    .WithHttpEndpoint(port: 4200, env: "PORT", isProxied: false)
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "Ghosts UI";
        url.Url = "http://localhost:4200/";
    });
//.WithNpmPackageInstallation();

builder.Build().Run();
