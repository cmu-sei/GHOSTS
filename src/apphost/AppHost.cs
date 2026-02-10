var builder = DistributedApplication.CreateBuilder(args);

var postgresUsername = builder.AddParameter("PostgresUsername", "ghosts", false);
var postgresPassword = builder.AddParameter("PostgresPassword", "scotty@1", false);

var postgres = builder.AddPostgres("postgres", userName: postgresUsername, password: postgresPassword)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("ghosts-postgres")
    .WithPgAdmin(pgAdmin =>
    {
        pgAdmin.WithEndpoint("http", endpoint => endpoint.Port = 33000);
    });

var db = postgres.AddDatabase("ghosts");
var dbPandora = postgres.AddDatabase("pandora");

var qdrant = builder.AddContainer("qdrant", "qdrant/qdrant")
    .WithContainerName("qdrant")
    .WithHttpEndpoint(port: 6333, targetPort: 6333, name: "http");

var facebook = builder.AddContainer("facebook", "dustinupdyke/ghosts-pandora")
    .WithContainerName("facebook")
    .WaitFor(postgres)
    .WithEnvironment(
         "ConnectionStrings__Default",
        $"Host=postgres;Port=5432;Database=pandora;Username={postgresUsername};Password={postgresPassword}"
    )
    .WithHttpEndpoint(port: 8800, targetPort: 5000, name: "http")
    .WithEnvironment("MODE_TYPE", "social")
    .WithEnvironment("DEFAULT_THEME", "facebook");

var api = builder.AddProject<Projects.Ghosts_Api>("api")
    .WaitFor(postgres)
    .WithReference(db, "DefaultConnection")
    .WithEnvironment("ConnectionStrings__Provider", "PostgreSQL");

// Configure n8n with service reference to API
var n8n = builder.AddContainer("n8n", "docker.n8n.io/n8nio/n8n")
    .WithHttpEndpoint(port: 5678, targetPort: 5678, name: "http")
    .WithEnvironment("N8N_SECURITY_ALLOW_CROSS_ORIGIN", "*")
    .WithEnvironment("N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE", "true")
    .WithEnvironment("N8N_USER_MANAGEMENT_DISABLED", "true")
    .WithEnvironment("N8N_SECURE_COOKIE", "false")
    .WithEnvironment("N8N_ENABLE_API", "true")
    .WithEnvironment("N8N_SECURITY_ENABLE_OPENAPI", "true")
    .WithEnvironment("N8N_PORT", "5678")
    .WithEnvironment("N8N_PROTOCOL", "http")
    .WithEnvironment("N8N_DIAGNOSTICS_ENABLED", "false")
    .WithContainerName("n8n")
    .WithBindMount("n8n_data", "/home/node/.n8n", isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent);

// Add service discovery so n8n can reach the API
n8n.WithReference(api);

// Configure API with n8n reference
api.WithEnvironment(ctx =>
    {
        ctx.EnvironmentVariables["N8N_API_URL"] = n8n.GetEndpoint("http").Url + "/api/v1/workflows";
    })
    .WithEnvironment("N8N_API_KEY", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJlNzdmMTRjOS04YWEwLTQyMzItYTMwMi1mYmI4ZTFjYTIzZjEiLCJpc3MiOiJuOG4iLCJhdWQiOiJwdWJsaWMtYXBpIiwiaWF0IjoxNzcwNzI5NjgyfQ.EzAB89jNWBYgFMwc3CLQ8osZcmv99soIk-k2L5iRZ00");

var frontend = builder.AddJavaScriptApp("frontend", "../ghosts.ng", "start")
    .WithHttpEndpoint(port: 4200, env: "PORT", isProxied: false)
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "Ghosts UI";
        url.Url = "http://localhost:4200/";
    });
//.WithNpmPackageInstallation();

builder.Build().Run();
