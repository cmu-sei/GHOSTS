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
builder.Services.AddSingleton<KindCatalog>();
builder.Services.AddTransient<KindDrafter>();

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
    var kinds = scope.ServiceProvider.GetRequiredService<KindCatalog>();
    await KindStore.Seed(db, Path.GetFullPath(app.Configuration["Kinds:Directory"] ?? "../../kinds", app.Environment.ContentRootPath), CancellationToken.None);
    await KindStore.Load(db, kinds, CancellationToken.None);
    var libraries = Path.GetFullPath(app.Configuration["Libraries:Directory"] ?? "../../libraries", app.Environment.ContentRootPath);
    await LibrarySeeder.Seed(db, kinds, libraries, CancellationToken.None);
}

app.MapApiEndpoints();
app.Run();

public partial class Program;
