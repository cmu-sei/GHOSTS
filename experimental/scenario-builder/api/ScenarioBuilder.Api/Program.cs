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

// Filled from the database at startup; publishing adds to it.
builder.Services.AddSingleton<ScenarioTypeCatalog>();
builder.Services.AddTransient<ScenarioTypeDrafter>();

builder.Services.AddDbContext<BuilderDb>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Builder") ?? "Data Source=scenario-builder.db"));

builder.Services.Configure<ModelOptions>(builder.Configuration.GetSection(ModelOptions.Section));
builder.Services.AddHttpClient(ChatCompletionsEndpoint.Client)
    .ConfigureHttpClient((sp, client) =>
        client.Timeout = TimeSpan.FromMinutes(sp.GetRequiredService<IOptions<ModelOptions>>().Value.TimeoutMinutes));

builder.Services.AddHttpClient(SourceText.WebClient, client => client.Timeout = TimeSpan.FromSeconds(20));

builder.Services.AddHttpClient(GraySpaceClient.Client, client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<GraySpaceClient>();

builder.AddApiEndpoints();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BuilderDb>();
    db.Database.EnsureCreated();
    var scenarioTypes = scope.ServiceProvider.GetRequiredService<ScenarioTypeCatalog>();
    await ScenarioTypeStore.Seed(db, Path.GetFullPath(app.Configuration["ScenarioTypes:Directory"] ?? "../../scenario-types", app.Environment.ContentRootPath), CancellationToken.None);
    await ScenarioTypeStore.Load(db, scenarioTypes, CancellationToken.None);
    var libraries = Path.GetFullPath(app.Configuration["Libraries:Directory"] ?? "../../libraries", app.Environment.ContentRootPath);
    await LibrarySeeder.Seed(db, scenarioTypes, libraries, CancellationToken.None);
}

app.MapApiEndpoints();
app.Run();

public partial class Program;
