// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Sources;

/// <summary>POST /scenarios/{id}/sources/file — a document's text, read now; the file itself isn't kept.</summary>
public class UploadEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/sources/file", async (int id, IFormFile? file, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Scenarios.FindAsync([id], ct) is not { } scenario)
                    return ScenarioState.Missing(id);
                var caller = Caller.From(http);
                if (SourceRules.AgentRefused(caller) is { } refused)
                    return refused;
                if (file is null)
                    return ErrorResults.BadRequest("FILE_REQUIRED", "Choose a file to add.");
                var name = Path.GetFileName(file.FileName);
                if (file.Length > SourceText.MaxFileBytes)
                    return ErrorResults.TooLarge("SOURCE_TOO_LARGE", $"{name} is over {SourceText.MaxFileBytes / 1024 / 1024} MB. Add the parts that matter.");

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, ct);
                string? text;
                try
                {
                    text = SourceText.FromFile(stream.ToArray(), name);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    return ErrorResults.Unprocessable("SOURCE_UNREADABLE", $"{name} couldn't be read: {e.Message} Paste its text instead.");
                }
                return text is null
                    ? ErrorResults.Unsupported("SOURCE_UNSUPPORTED", $"{name} isn't a type the builder reads. It reads {string.Join(", ", SourceText.FileTypes)}.")
                    : await SourceRules.Save(db, scenario, caller, SourceKind.File, name, name, text, ct);
            })
            .WithTags(tag);
}
