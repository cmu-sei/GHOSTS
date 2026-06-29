using System.Runtime.InteropServices;

var basePassword = "Scotty@@1!";
var builder = DistributedApplication.CreateBuilder(args);

var postgresUsername = builder.AddParameter("PostgresUsername", "ghosts", false);
var postgresPassword = builder.AddParameter("PostgresPassword", basePassword, false);

var postgres = builder.AddPostgres("postgres", userName: postgresUsername, password: postgresPassword)
    .WithDataVolume("ghosts-postgres18-data")
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

// Workaround: SkiaSharp.NativeAssets.Linux 3.x native lib references FreeType symbols
// but doesn't declare libfreetype as a NEEDED dependency, so LD_PRELOAD is required on ARM64
var freetypeLib = RuntimeInformation.OSArchitecture == Architecture.Arm64
    ? "/lib/aarch64-linux-gnu/libfreetype.so.6"
    : "";

var facebook = builder.AddProject<Projects.Ghosts_Pandora>("facebook")
    .WaitFor(postgres)
    .WithReference(dbPandora, "PostgreSQL")
    .WithEnvironment("DATABASE_PROVIDER", "PostgreSQL")
    .WithEnvironment("MODE_TYPE", "social")
    .WithEnvironment("DEFAULT_THEME", "facebook")
    .WithEnvironment("LD_PRELOAD", freetypeLib)
    .WithHttpEndpoint(port: 8800, name: "http", isProxied: false)
    .WithExternalHttpEndpoints();

var api = builder.AddProject<Projects.Ghosts_Api>("api")
    .WaitFor(postgres)
    .WithReference(db, "DefaultConnection")
    .WithEnvironment("ConnectionStrings__Provider", "PostgreSQL");

// n8n uses a dedicated Postgres database for reproducible state
var n8nDb = postgres.AddDatabase("n8n-db", "n8n");

var n8nEncryptionKey = builder.AddParameter("N8nEncryptionKey", "ghosts-dev-encryption-key-change-in-prod", false);
var n8nOwnerPassword = builder.AddParameter("N8nOwnerPassword", basePassword, false);

// Configure n8n with Postgres backend
var n8n = builder.AddContainer("n8n", "docker.n8n.io/n8nio/n8n")
    .WithHttpEndpoint(port: 5678, targetPort: 5678, name: "http", isProxied: false)
    .WithEnvironment("DB_TYPE", "postgresdb")
    .WithEnvironment("DB_POSTGRESDB_HOST", "ghosts-postgres")
    .WithEnvironment("DB_POSTGRESDB_PORT", "5432")
    .WithEnvironment("DB_POSTGRESDB_DATABASE", "n8n")
    .WithEnvironment("DB_POSTGRESDB_USER", postgresUsername)
    .WithEnvironment("DB_POSTGRESDB_PASSWORD", postgresPassword)
    .WithEnvironment("N8N_ENCRYPTION_KEY", n8nEncryptionKey)
    .WithEnvironment("N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS", "false")
    .WithEnvironment("N8N_SECURE_COOKIE", "false")
    .WithEnvironment("N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE", "true")
    .WithEnvironment("N8N_ENABLE_API", "true")
    .WithEnvironment("N8N_DIAGNOSTICS_ENABLED", "false")
    .WithEnvironment("N8N_PORT", "5678")
    .WithEnvironment("N8N_PROTOCOL", "http")
    .WithContainerName("n8n")
    .WithBindMount("n8n_data", "/home/node/.n8n", isReadOnly: false)
    .WithBindMount("../../configuration/n8n-workflows", "/bootstrap/workflows", isReadOnly: true)
    .WithBindMount("../../configuration/n8n-bootstrap/scripts", "/bootstrap/scripts", isReadOnly: true)
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(postgres);

// Provisioner sidecar: creates owner account and imports workflows on first boot
var n8nProvisioner = builder.AddContainer("n8n-provisioner", "docker.n8n.io/n8nio/n8n")
    .WithEnvironment("DB_TYPE", "postgresdb")
    .WithEnvironment("DB_POSTGRESDB_HOST", "ghosts-postgres")
    .WithEnvironment("DB_POSTGRESDB_PORT", "5432")
    .WithEnvironment("DB_POSTGRESDB_DATABASE", "n8n")
    .WithEnvironment("DB_POSTGRESDB_USER", postgresUsername)
    .WithEnvironment("DB_POSTGRESDB_PASSWORD", postgresPassword)
    .WithEnvironment("N8N_ENCRYPTION_KEY", n8nEncryptionKey)
    .WithEnvironment("N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS", "false")
    .WithEnvironment("N8N_OWNER_EMAIL", "ranger@admin.local")
    .WithEnvironment("N8N_OWNER_FIRST_NAME", "Ranger")
    .WithEnvironment("N8N_OWNER_LAST_NAME", "Admin")
    .WithEnvironment("N8N_OWNER_PASSWORD", n8nOwnerPassword)
    .WithEntrypoint("/bin/sh")
    .WithArgs("/bootstrap/scripts/provision-n8n.sh")
    .WithBindMount("n8n_data", "/home/node/.n8n", isReadOnly: false)
    .WithBindMount("../../configuration/n8n-workflows", "/bootstrap/workflows", isReadOnly: true)
    .WithBindMount("../../configuration/n8n-bootstrap/scripts", "/bootstrap/scripts", isReadOnly: true)
    .WaitFor(n8n);

// n8n workflows reach the API via host.docker.internal (configured in workflow nodes),
// not via Aspire service discovery, so no WithReference needed here.

// Wire API to reach n8n for workflow execution — use host.docker.internal since
// the API project runs inside a devcontainer and n8n exposes port 5678 on the host
api.WithEnvironment("N8N_API_URL", "http://host.docker.internal:5678/api/v1/workflows")
    .WithEnvironment("N8N_API_KEY_FILE", Path.GetFullPath("n8n_data/.api_key"));

var grafana = builder.AddContainer("grafana", "grafana/grafana")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "http")
    .WithContainerName("grafana")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithBindMount("../../configuration/grafana/datasources", "/etc/grafana/provisioning/datasources", isReadOnly: true)
    .WithBindMount("../../configuration/grafana/dashboards", "/etc/grafana/provisioning/dashboards", isReadOnly: true)
    .WithVolume("grafana-data", "/var/lib/grafana")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
    .WithEnvironment("GF_AUTH_DISABLE_LOGIN_FORM", "true")
    .WithEnvironment("GF_SECURITY_ALLOW_EMBEDDING", "true")
    .WithEnvironment("GF_SECURITY_X_FRAME_OPTIONS", "")
    .WaitFor(postgres);

// GHOSTS Universal Clients (built from source, demo/testing)
var client1 = builder.AddDockerfile("ghosts-client-1", "../", "Dockerfile-client-universal")
    .WithContainerName("ghosts-client-1")
    .WithEnvironment("BASE_URL", "http://host.docker.internal:5000/api")
    .WithBindMount("../../configuration/clients/client-1/config", "/app/config", isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(api);

var client2 = builder.AddDockerfile("ghosts-client-2", "../", "Dockerfile-client-universal")
    .WithContainerName("ghosts-client-2")
    .WithEnvironment("BASE_URL", "http://host.docker.internal:5000/api")
    .WithBindMount("../../configuration/clients/client-2/config", "/app/config", isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(api);

var client3 = builder.AddDockerfile("ghosts-client-3", "../", "Dockerfile-client-universal")
    .WithContainerName("ghosts-client-3")
    .WithEnvironment("BASE_URL", "http://host.docker.internal:5000/api")
    .WithBindMount("../../configuration/clients/client-3/config", "/app/config", isReadOnly: false)
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(api);

var frontend = builder.AddJavaScriptApp("frontend", "../Ghosts.Frontend", "start")
    .WaitFor(api)
    .WithEnvironment("PORT", "4200")
    .WithHttpEndpoint(port: 4200, name: "frontend-http", isProxied: false)
    .WithExternalHttpEndpoints();

var docs = builder.AddExecutable("docs", "mkdocs", "../../", "serve", "--dev-addr=0.0.0.0:8000")
    .WithHttpEndpoint(port: 8000, name: "http", isProxied: false)
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "GHOSTS Docs";
        url.Url = "http://localhost:8000/";
    });

builder.Build().Run();
