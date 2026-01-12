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

var qdrant = builder.AddContainer("qdrant", "qdrant/qdrant")
    .WithContainerName("qdrant")
    .WithHttpEndpoint(port: 6333, targetPort: 6333, name: "http");

var n8n = builder.AddContainer("n8n", "docker.n8n.io/n8nio/n8n")
    .WithHttpEndpoint(port: 5678, targetPort: 5678, name: "http")
    .WithEnvironment("N8N_SECURITY_ALLOW_CROSS_ORIGIN", "*")
    .WithEnvironment("N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE", "true")
    .WithEnvironment("N8N_SECURE_COOKIE", "false")
    .WithEnvironment("N8N_ENABLE_API", "true")
    .WithEnvironment("N8N_SECURITY_ENABLE_OPENAPI", "true")
    .WithContainerName("n8n")
    .WithBindMount("n8n_data", "/home/node/.n8n", isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddProject<Projects.Ghosts_Api>("api")
    .WaitFor(postgres)
    .WithReference(db, "DefaultConnection")
    .WithEnvironment("ConnectionStrings__Provider", "PostgreSQL")
    .WithEnvironment(ctx =>
    {
        ctx.EnvironmentVariables["N8N_API_URL"] = n8n.GetEndpoint("http").Url + "/api/v1/workflows";
    })
    .WithEnvironment("N8N_API_KEY", "supersecretkey");

var frontend = builder.AddJavaScriptApp("frontend", "../ghosts.ng", "start")
    .WithHttpEndpoint(port: 4200, env: "PORT", isProxied: false)
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "Ghosts UI";
        url.Url = "http://localhost:4200/";
    });
//.WithNpmPackageInstallation();

builder.Build().Run();
