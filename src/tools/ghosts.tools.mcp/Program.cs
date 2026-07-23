using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args.Contains("--http", StringComparer.OrdinalIgnoreCase))
{
    var webBuilder = WebApplication.CreateBuilder(args);

    webBuilder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithTools<Ghosts.Tools.Mcp.GhostsTools>();

    var app = webBuilder.Build();

    app.MapGet("/", () => Results.Json(new
    {
        name = "GHOSTS MCP Server",
        mcp = "/mcp",
        ghostsApiBaseUrl = Ghosts.Tools.Mcp.GhostsApiClient.BaseUrl
    }));
    app.MapMcp("/mcp");

    await app.RunAsync();
}
else
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<Ghosts.Tools.Mcp.GhostsTools>();

    await builder.Build().RunAsync();
}
