// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IScenarioAuthoringService
    {
        Task<AuthoringSession> CreateSessionAsync(int? scenarioId, string model, CancellationToken ct);
        Task<IReadOnlyList<AuthoringSessionSummary>> SessionsAsync(int scenarioId, CancellationToken ct);
        Task<AuthoringSession> FindSessionAsync(Guid sessionId, CancellationToken ct);
        Task<AuthoringTurnResult> RunTurnAsync(Guid sessionId, string message, CancellationToken ct);
        Task<object> GetSessionAsync(Guid sessionId, bool running, CancellationToken ct);
        Task<object> GetDocumentAsync(Guid sessionId, string hash, CancellationToken ct);
        Task<object> GetChunkAsync(Guid sessionId, int chunkId, CancellationToken ct);
        Task<AuthoringImportResult> ImportAsync(Guid sessionId, string hash, bool again, bool replace, CancellationToken ct);
    }

    /// <summary>Where a turn reports as it runs: each model call and each tool call (section 10). Tests leave it out.</summary>
    public interface IAuthoringProgress
    {
        Task SendAsync(int? scenarioId, object progress);
    }

    public record AuthoringSessionSummary(Guid Id, string Model, int Turns, int? ImportedScenarioId, DateTime CreatedAt, DateTime UpdatedAt);

    public record AuthoringToolCall(string Name, bool Ok);

    public record AuthoringDocumentStatus(string Hash, int Turn, int Errors, int Warnings, bool Shown, int? ImportedScenarioId);

    /// <summary>
    /// One turn's result. On a failure Reply is empty, Failure names the cause (B5), and the gate report still
    /// lists what the turn did, including any document it validated and kept (C2). Status, Attention, Changes
    /// and Gaps are the server's lines for the page (B2, B3, D2, E5), never the model's.
    /// </summary>
    public record AuthoringTurnResult(
        int Turn,
        string Reply,
        string GateReport,
        IReadOnlyList<AuthoringToolCall> ToolCalls,
        IReadOnlyList<AuthoringDocumentStatus> Validations,
        AuthoringDocumentStatus LatestDocument,
        bool CanImport,
        AuthoringFailure Failure,
        string Status = null,
        IReadOnlyList<string> Attention = null,
        IReadOnlyList<string> Changes = null,
        IReadOnlyList<string> Gaps = null,
        string Model = null);

    /// <summary>Cause is "model error", "timeout" or "empty reply".</summary>
    public record AuthoringFailure(string Cause, string Notice, string Details);

    /// <summary>Confirm names what a refusal needs confirmed before the import is sent again: "again" (A4) or "replace".</summary>
    public record AuthoringImportResult(bool Imported, int? ScenarioId, string Hash, string Reason, bool NeedsConfirmation, string Report,
        string Confirm = null);

    public class AuthoringSessionBusyException(Guid id) : Exception($"A turn or an import is already running in session {id}.");

    /// <summary>
    /// Scenario authoring as a service of the API: the agent loop of the n8n prototype (GhScenAuthor0004),
    /// with the server's checks around it. The model drafts and validates; the server keeps the record, writes
    /// the gate report, and imports only on the developer's action.
    /// </summary>
    public class ScenarioAuthoringService : IScenarioAuthoringService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        // One turn or import at a time per session; sessions run side by side (C6).
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

        private static readonly Lazy<string> DefaultPrompt = new(() => File.ReadAllText(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "config", "ContentServices", "ScenarioAuthoring", "system-prompt.md")));

        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        private readonly ApplicationDbContext _context;
        private readonly IScenarioService _scenarios;
        private readonly IAuthoringModel _model;
        private readonly IHttpClientFactory _clients;
        private readonly ScenarioAuthoringOptions _options;
        private readonly ScenarioAuthoringTools _tools;
        private readonly string _prompt;
        private readonly IAuthoringProgress _progress;

        public ScenarioAuthoringService(
            ApplicationDbContext context,
            IScenarioService scenarios,
            IAuthoringModel model,
            IServiceScopeFactory scopes,
            IHttpClientFactory clients,
            IOptions<ScenarioAuthoringOptions> options,
            string systemPrompt = null,
            IAuthoringProgress progress = null)
        {
            _context = context;
            _scenarios = scenarios;
            _model = model;
            _clients = clients;
            _options = options.Value;
            _tools = new ScenarioAuthoringTools(context, scopes, clients, TimeSpan.FromSeconds(_options.ValidatorTimeoutSeconds));
            _prompt = systemPrompt;
            _progress = progress;
        }

        /// <summary>A turn's limit, as configured in minutes. A test sets a shorter one.</summary>
        public TimeSpan? TurnLimitOverride { get; set; }

        /// <summary>
        /// A session for the Scenario Builder's scenario, which its import replaces; with none, the import makes a new one.
        /// Its model is picked here, from the configured ones, and kept for the whole session (C5).
        /// </summary>
        public async Task<AuthoringSession> CreateSessionAsync(int? scenarioId, string model, CancellationToken ct)
        {
            if (scenarioId != null && !await _context.Scenarios.AnyAsync(s => s.Id == scenarioId, ct))
                throw new KeyNotFoundException($"No scenario {scenarioId}.");

            if (string.IsNullOrWhiteSpace(model)) model = _options.Model;
            if (model != _options.Model && _options.Models.All(m => m.Id != model))
                throw new ArgumentException($"{model} is not one of the configured models.");

            var session = new AuthoringSession { Id = Guid.NewGuid(), Model = model, ScenarioId = scenarioId };
            _context.AuthoringSessions.Add(session);
            await _context.SaveChangesAsync(ct);
            return session;
        }

        /// <summary>A scenario's sessions, newest first, so the Scenario Builder can resume the latest (section 2, step 7).</summary>
        public async Task<IReadOnlyList<AuthoringSessionSummary>> SessionsAsync(int scenarioId, CancellationToken ct) =>
            await _context.AuthoringSessions.AsNoTracking()
                .Where(s => s.ScenarioId == scenarioId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new AuthoringSessionSummary(s.Id, s.Model, s.Turns.Count, s.ImportedScenarioId, s.CreatedAt, s.UpdatedAt))
                .ToListAsync(ct);

        public async Task<AuthoringSession> FindSessionAsync(Guid sessionId, CancellationToken ct) =>
            await _context.AuthoringSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        // ───────────── a turn ─────────────

        public async Task<AuthoringTurnResult> RunTurnAsync(Guid sessionId, string message, CancellationToken ct)
        {
            var gate = Locks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
            if (!await gate.WaitAsync(0, ct)) throw new AuthoringSessionBusyException(sessionId);
            try
            {
                return await RunTurnLockedAsync(sessionId, message, ct);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<AuthoringTurnResult> RunTurnLockedAsync(Guid sessionId, string message, CancellationToken ct)
        {
            var session = await _context.AuthoringSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
                          ?? throw new KeyNotFoundException($"No authoring session {sessionId}.");

            var turn = (await _context.AuthoringMessages.Where(m => m.SessionId == sessionId)
                .MaxAsync(m => (int?)m.Turn, ct) ?? 0) + 1;

            // The conversation the model sees: every kept message, in order (C3, C4).
            var stored = await _context.AuthoringMessages.AsNoTracking()
                .Where(m => m.SessionId == sessionId && m.InHistory)
                .OrderBy(m => m.Turn).ThenBy(m => m.Sequence)
                .ToListAsync(ct);
            var conversation = stored.Select(m => new Message
            {
                Role = m.Role == AuthoringMessage.ModelRole ? ConversationRole.Assistant : ConversationRole.User,
                Content = AuthoringBlocks.FromJson(m.Content)
            }).ToList();

            // The developer's message, after anything the server has to tell the model first.
            var opening = new List<ContentBlock>();
            if (!string.IsNullOrWhiteSpace(session.PendingNote)) opening.Add(new ContentBlock { Text = session.PendingNote });
            opening.Add(new ContentBlock { Text = message });
            conversation.Add(new Message { Role = ConversationRole.User, Content = opening });

            var rows = new List<AuthoringMessage>();
            AddRow(rows, sessionId, turn, AuthoringMessage.Developer, opening);

            // What a reload shows while the turn runs, and its result once it ends.
            var record = new AuthoringTurn { SessionId = sessionId, Turn = turn, Message = message };
            _context.AuthoringTurns.Add(record);
            await _context.SaveChangesAsync(CancellationToken.None);
            await ReportAsync(session, turn, "turn-started");

            var calls = new List<AuthoringToolCall>();
            var validated = new List<AuthoringDocument>();

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(TurnLimitOverride ?? TimeSpan.FromMinutes(_options.TurnTimeoutMinutes));

            string reply = null;
            AuthoringFailure failure = null;
            try
            {
                while (true)
                {
                    var response = await CallModelAsync(session.Model, conversation, rows, sessionId, turn, limit.Token);
                    var content = response.Output?.Message?.Content ?? [];
                    await ReportAsync(session, turn, "model-reply", new { stopReason = response.StopReason?.Value });
                    conversation.Add(new Message { Role = ConversationRole.Assistant, Content = content });

                    var uses = content.Where(b => b.ToolUse != null).Select(b => b.ToolUse).ToList();
                    if (uses.Count == 0)
                    {
                        // The reply's text is the first block that holds text, whatever comes before it.
                        reply = content.FirstOrDefault(b => b.Text != null)?.Text;
                        break;
                    }

                    var results = new List<ContentBlock>();
                    foreach (var use in uses)
                    {
                        var outcome = await _tools.RunAsync(use.Name, AuthoringBlocks.ToJson(use.Input), session, limit.Token);
                        calls.Add(new AuthoringToolCall(use.Name, outcome.Ok));
                        if (outcome.Validation != null) validated.Add(await KeepAsync(sessionId, turn, outcome.Validation));
                        await ReportAsync(session, turn, "tool-call", new
                        {
                            name = use.Name,
                            ok = outcome.Ok,
                            hash = outcome.Validation?.Hash,
                            errors = outcome.Validation?.Errors,
                            warnings = outcome.Validation?.Warnings
                        });

                        results.Add(new ContentBlock
                        {
                            ToolResult = new ToolResultBlock
                            {
                                ToolUseId = use.ToolUseId,
                                Status = outcome.Ok ? ToolResultStatus.Success : ToolResultStatus.Error,
                                Content = [new ToolResultContentBlock { Text = outcome.Text }]
                            }
                        });
                    }
                    AddRow(rows, sessionId, turn, AuthoringMessage.Tool, results);
                    conversation.Add(new Message { Role = ConversationRole.User, Content = results });
                }

                if (string.IsNullOrWhiteSpace(reply))
                    failure = Failed("empty reply", "The model returned an empty reply.");
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                failure = Failed("timeout",
                    $"The turn reached its limit of {(TurnLimitOverride ?? TimeSpan.FromMinutes(_options.TurnTimeoutMinutes)).TotalMinutes:0.##} minutes, and the model call it was waiting on was cancelled.");
            }
            catch (AuthoringModelException ex)
            {
                failure = Failed("model error", ex.Message);
            }

            // Kept for the record either way (H1); only a turn that succeeded joins the conversation (C4). A model
            // call that failed and was retried has no reply to send back, so it never joins.
            foreach (var row in rows) row.InHistory = failure == null && row.Error == null;
            _context.AuthoringMessages.AddRange(rows);

            var documents = await _context.AuthoringDocuments.Where(d => d.SessionId == sessionId)
                .OrderBy(d => d.Id).ToListAsync(CancellationToken.None);
            var latest = documents.LastOrDefault();

            // A3: the server returns a document with the reply of the turn that validated it, so a reply that came
            // back shows what its turn validated. A failed or empty turn shows nothing; the developer shows such a
            // document by opening it on the panel (GetDocumentAsync).
            if (failure == null)
            {
                var validatedNow = validated.Select(d => d.Hash).ToHashSet();
                foreach (var d in documents.Where(d => !d.Shown && validatedNow.Contains(d.Hash)))
                    d.Shown = true;
            }

            var gaps = Gaps(latest);
            var report = GateReport(turn, calls, validated, latest, documents, failure, gaps);

            // A turn that succeeded delivered the note; the next one gets this turn's gate report. A failed turn
            // delivered nothing, so its note stays for the next try, with what it validated: the model never saw it.
            if (failure == null)
                session.PendingNote = "Server note (from the GHOSTS server, not the developer). The gate report of your previous turn:\n" + report;
            else if (validated.Count > 0)
                AppendNote(session,
                    $"The developer's previous message failed ({failure.Cause}) and was not kept, so you did not see that turn. " +
                    $"It validated {validated.Count} document(s), which the developer has not been shown: " +
                    string.Join("; ", validated.Select(d => $"{d.Hash} ({d.Errors} errors, {d.Warnings} warnings)")) + ".");
            session.UpdatedAt = DateTime.UtcNow;

            var importedBefore = latest != null && await ImportedBeforeAsync(latest.Hash);
            var last = validated.LastOrDefault();
            var result = new AuthoringTurnResult(
                turn,
                failure == null ? reply : string.Empty,
                report,
                calls,
                validated.Select(Status).ToList(),
                latest == null ? null : Status(latest),
                ImportRefusal(latest) == null && !importedBefore,
                failure,
                StatusLine(latest, importedBefore, failure),
                Attention(last),
                last == null ? [] : JsonSerializer.Deserialize<List<string>>(last.Changes) ?? [],
                gaps,
                session.Model);

            record.Result = JsonSerializer.Serialize(result, Web);
            record.EndedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(CancellationToken.None);

            return result;
        }

        private async Task ReportAsync(AuthoringSession session, int turn, string kind, object detail = null)
        {
            if (_progress == null) return;
            await _progress.SendAsync(session.ScenarioId, new { sessionId = session.Id, turn, kind, detail, at = DateTime.UtcNow });
        }

        private static AuthoringFailure Failed(string cause, string details) => new(
            cause,
            "This turn failed, so there is no reply, and your message was not kept. Send it again.",
            details);

        /// <summary>
        /// One model call, retried once on HTTP 503 (F4). Every attempt is a row with its usage and stop
        /// reason from the provider's response (H1); a failed attempt records its error instead.
        /// </summary>
        private async Task<ConverseResponse> CallModelAsync(string model, List<Message> conversation,
            List<AuthoringMessage> rows, Guid sessionId, int turn, CancellationToken limit)
        {
            for (var attempt = 1; ; attempt++)
            {
                var request = new ConverseRequest
                {
                    ModelId = model,
                    System = [new SystemContentBlock { Text = _prompt ?? DefaultPrompt.Value }],
                    Messages = conversation,
                    ToolConfig = ScenarioAuthoringTools.Configuration(),
                    // Temperature is not sent: newer Anthropic models on Bedrock reject it.
                    InferenceConfig = new InferenceConfiguration { MaxTokens = _options.MaxOutputTokens }
                };

                var row = new AuthoringMessage
                {
                    SessionId = sessionId, Turn = turn, Sequence = rows.Count, Role = AuthoringMessage.ModelRole,
                    StartedAt = DateTime.UtcNow
                };
                rows.Add(row);
                try
                {
                    var response = await _model.ConverseAsync(request, limit);
                    row.EndedAt = DateTime.UtcNow;
                    row.Content = AuthoringBlocks.ToJson(response.Output?.Message?.Content).ToJsonString();
                    row.InputTokens = response.Usage?.InputTokens;
                    row.OutputTokens = response.Usage?.OutputTokens;
                    row.CacheReadTokens = response.Usage?.CacheReadInputTokens;
                    row.CacheWriteTokens = response.Usage?.CacheWriteInputTokens;
                    row.StopReason = response.StopReason?.Value;
                    return response;
                }
                catch (OperationCanceledException) when (limit.IsCancellationRequested)
                {
                    row.EndedAt = DateTime.UtcNow;
                    row.Error = "Cancelled: the turn reached its limit.";
                    throw;
                }
                catch (Exception ex)
                {
                    row.EndedAt = DateTime.UtcNow;
                    row.Error = $"{ex.GetType().Name}: {ex.Message}";
                    _log.Warn($"Authoring model call failed, session {sessionId}, turn {turn}, attempt {attempt}: {row.Error}");
                    if (attempt == 1 && IsUnavailable(ex)) continue;
                    throw new AuthoringModelException(row.Error, ex);
                }
            }
        }

        private static bool IsUnavailable(Exception ex) =>
            ex is ServiceUnavailableException
            || (ex is AmazonServiceException s && (int)s.StatusCode == 503);

        /// <summary>C2: a validation is kept the moment it returns, inside the turn, whatever happens next.</summary>
        private async Task<AuthoringDocument> KeepAsync(Guid sessionId, int turn, AuthoringValidation v)
        {
            var document = new AuthoringDocument
            {
                SessionId = sessionId,
                Turn = turn,
                Hash = v.Hash,
                Document = v.Document,
                Errors = v.Errors,
                Warnings = v.Warnings,
                Findings = JsonSerializer.Serialize(v.Findings, Web),
                DryRun = v.DryRun,
                BaseHash = v.BaseHash,
                Changes = JsonSerializer.Serialize(v.Changes ?? [], Web)
            };
            _context.AuthoringDocuments.Add(document);
            await _context.SaveChangesAsync(CancellationToken.None);
            return document;
        }

        private static void AddRow(List<AuthoringMessage> rows, Guid sessionId, int turn, string role, IEnumerable<ContentBlock> content) =>
            rows.Add(new AuthoringMessage
            {
                SessionId = sessionId, Turn = turn, Sequence = rows.Count, Role = role,
                Content = AuthoringBlocks.ToJson(content).ToJsonString()
            });

        private static AuthoringDocumentStatus Status(AuthoringDocument d) =>
            new(d.Hash, d.Turn, d.Errors, d.Warnings, d.Shown, d.ImportedScenarioId);

        /// <summary>
        /// The import rule, for the turn's canImport and for the import endpoint alike (A2, A3): the session's
        /// latest validated document, validated with 0 errors, and shown. Null when it holds, otherwise why not.
        /// The endpoint also checks that the hash it was given is that document's; a hash imported before is a
        /// separate, second gate (A4).
        /// </summary>
        private static string ImportRefusal(AuthoringDocument latest) =>
            latest == null ? "Nothing has been validated in this session."
            : latest.Errors != 0 ? $"{latest.Hash} did not pass validation ({latest.Errors} errors)."
            : !latest.Shown ? $"{latest.Hash} is not shown: it was validated on turn {latest.Turn}, whose reply did not come back. " +
                              "Open the document on the panel to show it, or have it validated again in a turn that returns."
            : null;

        private async Task<bool> ImportedBeforeAsync(string hash) =>
            await _context.AuthoringDocuments.AsNoTracking().AnyAsync(d => d.Hash == hash && d.ImportedScenarioId != null);

        /// <summary>B2: the page's one status line, from the server's record of the latest document.</summary>
        private static string StatusLine(AuthoringDocument latest, bool importedBefore, AuthoringFailure failure)
        {
            var prefix = failure == null ? string.Empty : $"This turn failed ({failure.Cause}). ";
            if (latest == null) return prefix + "No document validated yet.";

            var counts = $"Document {latest.Hash}: {latest.Errors} errors, {latest.Warnings} warnings.";
            return prefix + (latest.ImportedScenarioId != null ? $"{counts} Imported as scenario {latest.ImportedScenarioId}."
                : latest.Errors != 0 ? $"{counts} It cannot be imported until it validates with 0 errors."
                : !latest.Shown ? $"{counts} Open it to show it before it can be imported."
                : importedBefore ? $"{counts} Imported before: importing it again asks first."
                : $"{counts} Ready to import.");
        }

        /// <summary>B3: one plain sentence per kind of warning in the turn's last validation that needs the developer.</summary>
        private static List<string> Attention(AuthoringDocument last)
        {
            if (last == null) return [];
            var findings = JsonSerializer.Deserialize<List<JsonObject>>(last.Findings) ?? [];
            return findings
                .Where(f => f["severity"]?.GetValue<string>() == ScenarioFinding.Warning)
                .GroupBy(f => f["code"]?.GetValue<string>())
                .Select(g => g.Key switch
                {
                    "REF_UNSET_FLAG" => $"{g.Count()} conditions read a flag that nothing in the document sets. Check that each one is a flag the white cell raises during play, not a typo.",
                    "FLAG_NEVER_READ" => $"{g.Count()} flags are set but never read. Add the condition that should read each one, or remove the flag.",
                    _ => $"{g.Count()} × {g.Key}: see Details."
                })
                .ToList();
        }

        /// <summary>E5: what the latest document's exercise still lacks; nothing for a document that does not parse.</summary>
        private static List<string> Gaps(AuthoringDocument latest)
        {
            if (latest == null) return [];
            try
            {
                return JsonNode.Parse(latest.Document) is JsonObject doc ? ScenarioCompleteness.Gaps(doc) : [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// B1: the server's account of the turn, from the tool calls it ran and the validator's results, never
        /// from the model's text.
        /// </summary>
        private static string GateReport(int turn, IReadOnlyList<AuthoringToolCall> calls, IReadOnlyList<AuthoringDocument> validated,
            AuthoringDocument latest, IReadOnlyList<AuthoringDocument> documents, AuthoringFailure failure, IReadOnlyList<string> gaps)
        {
            var lines = new List<string> { $"Gate report, turn {turn} (from the server's tool calls, not the agent)" };
            if (failure != null) lines.Add($"- The turn failed ({failure.Cause}); it was not kept. {failure.Details}");

            lines.Add(calls.Count == 0
                ? "- Tool calls this turn: none."
                : $"- Tool calls this turn ({calls.Count}): {string.Join(", ", calls.Select(c => c.Ok ? c.Name : $"{c.Name} (failed)"))}. Count of each: " +
                  string.Join(", ", calls.GroupBy(c => c.Name).Select(g => $"{g.Key} {g.Count()}")) + ".");

            if (validated.Count == 0)
            {
                lines.Add("- No validate call this turn.");
            }
            else
            {
                lines.Add($"- Validate calls this turn ({validated.Count}), in order:");
                for (var i = 0; i < validated.Count; i++)
                {
                    var v = validated[i];
                    lines.Add($"  - #{i + 1} {v.Hash} | {v.Errors} errors | {v.Warnings} warnings{(v.DryRun ? " | dry run" : string.Empty)}" +
                              (v.BaseHash == null ? string.Empty : $" | compared with {v.BaseHash}"));
                }
                var last = validated[^1];
                var findings = JsonSerializer.Deserialize<List<JsonObject>>(last.Findings) ?? [];
                lines.Add($"- Last validate this turn: {last.Errors} errors, {last.Warnings} warnings, {findings.Count} findings:");
                foreach (var f in findings)
                    lines.Add($"  - {f["code"]} | {f["severity"]} | {f["path"]} | {f["message"]}");
            }

            if (latest == null)
                lines.Add("- No document validated yet in this session.");
            else if (latest.ImportedScenarioId != null)
                lines.Add($"- Document {latest.Hash} was imported as scenario {latest.ImportedScenarioId}.");
            else if (latest.Errors != 0)
                lines.Add($"- The latest document, {latest.Hash} (turn {latest.Turn}), has {latest.Errors} errors, so it cannot be imported.");
            else if (!latest.Shown)
                lines.Add($"- Document {latest.Hash} was validated on turn {latest.Turn}, but the developer has not been shown it, so it cannot be imported yet.");
            else if (documents.Any(d => d.Hash == latest.Hash && d.ImportedScenarioId != null))
                lines.Add($"- Document {latest.Hash} was imported before; importing it again needs confirmation.");
            else
                lines.Add($"- Document {latest.Hash} (validated on turn {latest.Turn}) can be imported with the Import button.");

            if (latest != null)
                lines.Add(gaps.Count == 0
                    ? "- The exercise has objectives with pass conditions, an audience, rules of engagement and rules of play."
                    : $"- What the exercise still lacks ({gaps.Count}; reported, not blocking): {string.Join(" ", gaps)}");

            return string.Join("\n", lines);
        }

        // ───────────── the record ─────────────

        /// <summary>
        /// What the panel shows: the turns with their results, the latest document's state, and each document's
        /// counts. A document's text is not here, nor are the messages' blocks, which hold the agent's drafts:
        /// returning either would show a document the developer has not opened (A3). GetDocumentAsync returns it.
        /// </summary>
        public async Task<object> GetSessionAsync(Guid sessionId, bool running, CancellationToken ct)
        {
            var session = await _context.AuthoringSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct);
            if (session == null) return null;

            var turns = await _context.AuthoringTurns.AsNoTracking().Where(t => t.SessionId == sessionId)
                .OrderBy(t => t.Turn).ToListAsync(ct);
            var messages = await _context.AuthoringMessages.AsNoTracking().Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.Turn).ThenBy(m => m.Sequence).ToListAsync(ct);
            var documents = await _context.AuthoringDocuments.AsNoTracking().Where(d => d.SessionId == sessionId)
                .OrderBy(d => d.Id).ToListAsync(ct);
            var calls = messages.Where(m => m.Role == AuthoringMessage.ModelRole).ToList();
            var latest = documents.LastOrDefault();
            var importedBefore = latest != null && await ImportedBeforeAsync(latest.Hash);

            return new
            {
                session.Id,
                session.Model,
                session.Status,
                session.ScenarioId,
                session.ImportedScenarioId,
                session.CreatedAt,
                session.UpdatedAt,
                running,
                turns = turns.Select(t => new
                {
                    t.Turn,
                    t.Message,
                    t.StartedAt,
                    t.EndedAt,
                    // A turn with no end that is not running was cut off by a restart.
                    interrupted = t.EndedAt == null && !(running && t.Turn == turns[^1].Turn),
                    result = t.Result == null ? null : JsonSerializer.Deserialize<AuthoringTurnResult>(t.Result, Web)
                }),
                latestDocument = latest == null ? null : Status(latest),
                canImport = ImportRefusal(latest) == null && !importedBefore,
                statusLine = StatusLine(latest, importedBefore, null),
                gaps = Gaps(latest),
                documents = documents.Select(d => new
                {
                    d.Turn,
                    d.Hash,
                    d.BaseHash,
                    d.Errors,
                    d.Warnings,
                    d.DryRun,
                    d.Shown,
                    d.ImportedScenarioId,
                    d.CreatedAt
                }),
                messages = messages.Select(m => new
                {
                    m.Turn,
                    m.Sequence,
                    m.Role,
                    m.InHistory,
                    m.InputTokens,
                    m.OutputTokens,
                    m.CacheReadTokens,
                    m.CacheWriteTokens,
                    m.StopReason,
                    m.Error,
                    m.StartedAt,
                    m.EndedAt
                }),
                tokens = new
                {
                    modelCalls = calls.Count,
                    input = calls.Sum(m => m.InputTokens ?? 0),
                    output = calls.Sum(m => m.OutputTokens ?? 0),
                    cacheRead = calls.Sum(m => m.CacheReadTokens ?? 0),
                    cacheWrite = calls.Sum(m => m.CacheWriteTokens ?? 0)
                }
            };
        }

        /// <summary>
        /// Returns a validated document to the developer, with its findings and change list, and records that it
        /// was shown (A3): this is how a document from a turn that failed becomes importable.
        /// </summary>
        public async Task<object> GetDocumentAsync(Guid sessionId, string hash, CancellationToken ct)
        {
            hash = hash?.Trim().ToLowerInvariant();
            var document = await _context.AuthoringDocuments
                .Where(d => d.SessionId == sessionId && d.Hash == hash)
                .OrderByDescending(d => d.Id)
                .FirstOrDefaultAsync(ct);
            if (document == null) return null;

            if (!document.Shown)
            {
                document.Shown = true;
                await _context.SaveChangesAsync(CancellationToken.None);
            }

            return new
            {
                document.Turn,
                document.Hash,
                document.BaseHash,
                document.Errors,
                document.Warnings,
                document.Shown,
                document.ImportedScenarioId,
                findings = JsonSerializer.Deserialize<List<ScenarioFinding>>(document.Findings, Web),
                changes = JsonSerializer.Deserialize<List<string>>(document.Changes, Web),
                gaps = Gaps(document),
                document = document.Document
            };
        }

        /// <summary>J2: the text of a chunk a reply cites, when it is one of the session's scenario's sources.</summary>
        public async Task<object> GetChunkAsync(Guid sessionId, int chunkId, CancellationToken ct)
        {
            var scenarioId = await _context.AuthoringSessions.AsNoTracking().Where(s => s.Id == sessionId)
                .Select(s => s.ScenarioId).FirstOrDefaultAsync(ct);
            var chunk = scenarioId == null ? null : await _context.ScenarioSourceChunks.AsNoTracking()
                .Include(c => c.Source)
                .FirstOrDefaultAsync(c => c.Id == chunkId && c.ScenarioId == scenarioId, ct);

            return chunk == null ? null : new
            {
                chunkId = chunk.Id,
                chunk.SourceId,
                source = chunk.Source?.Name,
                index = chunk.ChunkIndex,
                text = chunk.Content
            };
        }

        // ───────────── the import ─────────────

        /// <summary>
        /// The only way from a session to the database (A1). Only the session's latest document, validated with
        /// 0 errors and shown (A2, A3); its exact bytes are validated again, then imported by the same steps as
        /// POST api/scenarios/import. A session of the Scenario Builder imports into its scenario, replacing what
        /// it held; that needs replace: true when it holds anything. A hash imported before needs again: true (A4).
        /// </summary>
        public async Task<AuthoringImportResult> ImportAsync(Guid sessionId, string hash, bool again, bool replace, CancellationToken ct)
        {
            var gate = Locks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
            if (!await gate.WaitAsync(0, ct)) throw new AuthoringSessionBusyException(sessionId);
            try
            {
                return await ImportLockedAsync(sessionId, hash?.Trim().ToLowerInvariant(), again, replace, ct);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<AuthoringImportResult> ImportLockedAsync(Guid sessionId, string hash, bool again, bool replace, CancellationToken ct)
        {
            var session = await _context.AuthoringSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
                          ?? throw new KeyNotFoundException($"No authoring session {sessionId}.");
            var documents = await _context.AuthoringDocuments.Where(d => d.SessionId == sessionId).OrderBy(d => d.Id).ToListAsync(ct);
            var latest = documents.LastOrDefault();

            string refusal = null;
            string confirm = null;
            if (string.IsNullOrEmpty(hash))
                refusal = "No hash was given.";
            else if (latest != null && hash != latest.Hash)
                refusal = documents.Any(d => d.Hash == hash)
                    ? $"{hash} is not the latest validated document. The latest is {latest.Hash}, from turn {latest.Turn}."
                    : $"{hash} is not a document of this session. The latest is {latest.Hash}, from turn {latest.Turn}.";
            else if (ImportRefusal(latest) is { } rule)
                refusal = rule;
            else
            {
                var before = await _context.AuthoringDocuments.AsNoTracking()
                    .Where(d => d.Hash == hash && d.ImportedScenarioId != null)
                    .Select(d => d.ImportedScenarioId).FirstOrDefaultAsync(ct);
                if (before != null && !again)
                {
                    refusal = $"{hash} was already imported as scenario {before}. To import it again, send the import again with \"again\": true.";
                    confirm = "again";
                }
                else if (session.ScenarioId is int into)
                {
                    if (!await _context.Scenarios.AnyAsync(s => s.Id == into, ct))
                        refusal = $"Scenario {into}, which this session imports into, no longer exists.";
                    else if (!replace && await _scenarios.HasContentAsync(into, ct))
                    {
                        refusal = $"Scenario {into} already holds a scenario, and importing {hash} replaces all of it: its timeline, adversaries, " +
                                  "population, objectives and graph. Its sources stay. To go ahead, send the import again with \"replace\": true.";
                        confirm = "replace";
                    }
                }
            }

            if (refusal != null) return await RefuseAsync(session, hash, refusal, confirm);

            // A2: the same bytes, validated again, then the import endpoint's own sequence.
            var document = JsonNode.Parse(latest.Document) as JsonObject;
            var result = await ScenarioDocumentValidator.ValidateAsync(document, _clients, ct);
            if (!result.IsValid)
                return await RefuseAsync(session, hash, $"{hash} no longer passes validation ({result.Errors} errors).", null);

            var gaps = ScenarioCompleteness.Gaps(document);
            var target = session.ScenarioId;
            var scenario = target != null
                ? await _scenarios.ReplaceFromDocumentAsync(target.Value, document, result.Findings, ct)
                : await _scenarios.ImportDocumentAsync(document, result.Findings, ct);

            // ImportDocumentAsync clears this context's change tracker, so the session's rows are read again.
            latest = await _context.AuthoringDocuments.FirstAsync(d => d.Id == latest.Id, CancellationToken.None);
            session = await _context.AuthoringSessions.FirstAsync(s => s.Id == sessionId, CancellationToken.None);
            latest.ImportedScenarioId = scenario.Id;
            session.ImportedScenarioId = scenario.Id;
            var report = "Import report (from the server, not the agent)\n" +
                         $"- Validated again: document {hash}, {result.Errors} errors, {result.Warnings} warnings, {result.Findings.Count} findings.\n" +
                         (target == null
                             ? $"- Imported: scenario id {scenario.Id}, document {hash}."
                             : $"- Imported: into scenario id {scenario.Id}, the Scenario Builder's scenario, replacing what it held; document {hash}.") +
                         (gaps.Count == 0 ? string.Empty : $"\n- What the exercise still lacks: {string.Join(" ", gaps)}");
            AppendNote(session, report);
            await _context.SaveChangesAsync(CancellationToken.None);
            _log.Info($"Authoring session {session.Id} imported document {latest.Hash} as scenario {scenario.Id}");

            return new AuthoringImportResult(true, scenario.Id, hash, null, false, report);
        }

        private async Task<AuthoringImportResult> RefuseAsync(AuthoringSession session, string hash, string reason, string confirm)
        {
            var report = $"Import report (from the server, not the agent)\n- {reason} Nothing was imported.";
            AppendNote(session, report);
            await _context.SaveChangesAsync(CancellationToken.None);
            return new AuthoringImportResult(false, null, hash, reason, confirm != null, report, confirm);
        }

        private static void AppendNote(AuthoringSession session, string text)
        {
            session.PendingNote = string.IsNullOrWhiteSpace(session.PendingNote)
                ? "Server note (from the GHOSTS server, not the developer).\n" + text
                : session.PendingNote + "\n\n" + text;
            session.UpdatedAt = DateTime.UtcNow;
        }
    }

    public class AuthoringModelException(string message, Exception inner) : Exception(message, inner);
}
