// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Serialization;
using ScenarioBuilder.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScenarioBuilder.Api.Endpoints;
using ScenarioBuilder.Api.Endpoints.Model;
using ScenarioBuilder.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Read when first resolved, so a test host's settings apply.
builder.Services.AddSingleton(sp =>
{
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    var directory = sp.GetRequiredService<IConfiguration>()["Kinds:Directory"] ?? "../../kinds";
    return new KindCatalog(Path.GetFullPath(directory, environment.ContentRootPath));
});

builder.Services.AddDbContext<BuilderDb>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Builder") ?? "Data Source=scenario-builder.db"));

builder.Services.Configure<ModelOptions>(builder.Configuration.GetSection(ModelOptions.Section));
builder.Services.AddHttpClient(ChatCompletionsEndpoint.Client)
    .ConfigureHttpClient((sp, client) =>
        client.Timeout = TimeSpan.FromMinutes(sp.GetRequiredService<IOptions<ModelOptions>>().Value.TimeoutMinutes));

builder.Services.AddHttpClient(SourceText.WebClient, client => client.Timeout = TimeSpan.FromSeconds(20));

builder.AddApiEndpoints();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BuilderDb>();
    db.Database.EnsureCreated();
    var libraries = Path.GetFullPath(app.Configuration["Libraries:Directory"] ?? "../../libraries", app.Environment.ContentRootPath);
    await LibrarySeeder.Seed(db, scope.ServiceProvider.GetRequiredService<KindCatalog>(), libraries, CancellationToken.None);
}

app.MapApiEndpoints();
app.Run();

public partial class Program;
