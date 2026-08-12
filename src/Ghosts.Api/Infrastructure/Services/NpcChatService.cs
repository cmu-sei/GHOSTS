// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.ContentServices;
using Ghosts.Api.Infrastructure.ContentServices.Bedrock;
using Ghosts.Api.Infrastructure.ContentServices.Ollama;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using NLog;

namespace Ghosts.Api.Infrastructure.Services;

public interface INpcChatService
{
    /// <summary>
    /// Answers an operator message in the voice of the NPC ("as") or as an assistant reporting on
    /// the NPC ("about"), looking up NPC state or dispatching a command when the message calls for it.
    /// Returns null when the NPC does not exist.
    /// </summary>
    Task<NpcChatResponse> Chat(Guid npcId, NpcChatRequest request, CancellationToken ct);

    Task<NpcChatConfig> GetConfig(CancellationToken ct);
}

/// <summary>
/// NPC chat is deliberately a two-call pipeline rather than an open agent loop: one JSON-schema
/// constrained call picks at most one tool, then one call writes the reply from a small persona card
/// plus the tool result. Small local models (gemma, phi, qwen at 4b-30b) are reliable at both of
/// those in isolation and unreliable at multi-step tool loops, so nothing here depends on the model
/// producing well-formed tool calls or remembering more than one instruction at a time.
/// </summary>
public class NpcChatService(
    INpcService npcService,
    IMachineService machineService,
    IMachineUpdateService machineUpdateService,
    IHubContext<ActivityHub> activityHubContext,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : INpcChatService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    private const string ToolNone = "none";
    private const string ToolRecentActivity = "recent_activity";
    private const string ToolMachineStatus = "machine_status";
    private const string ToolBrowseUrl = "browse_url";
    private const string ToolPostSocial = "post_social";

    /// <summary>
    /// Handler used when the operator tells an NPC to visit a site. Matches the handler the
    /// AI command endpoint favors.
    /// </summary>
    private const string BrowseHandler = "BrowserFirefox";

    private const int HistoryTurns = 8;
    private const int MaxTurnLength = 600;

    /// <summary>
    /// A message asks for two things at most in practice ("read x.com and post about it"). Capping the
    /// router keeps one message from turning into an open-ended run of commands.
    /// </summary>
    private const int MaxTools = 2;

    private static readonly Regex UrlPattern =
        new(@"(https?:\/\/)?([\w\-]+\.)+[\w]{2,}(\/\S*)?", RegexOptions.IgnoreCase);

    /// <summary>
    /// Display names for the platforms an operator is likely to name. Matched case-insensitively so
    /// the platform is read from the operator's own words instead of being invented by the model.
    /// </summary>
    private static readonly string[] SocialPlatforms =
    {
        "Facebook", "Twitter", "X.com", "LinkedIn", "Instagram", "Reddit", "Mastodon",
        "TikTok", "YouTube", "Discord", "Slack", "Teams", "Telegram", "WhatsApp"
    };

    /// <summary>
    /// Platform to Pandora theme. Anything without a theme of its own posts to the default one.
    /// </summary>
    private static readonly Dictionary<string, string> PlatformThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Facebook", "facebook" },
        { "Twitter", "x" },
        { "X.com", "x" },
        { "LinkedIn", "linkedin" },
        { "Instagram", "instagram" },
        { "Reddit", "reddit" },
        { "Discord", "discord" },
        { "YouTube", "youtube" }
    };

    /// <summary>
    /// Constrains the router response so even a 4b model cannot return unparseable output
    /// </summary>
    private static readonly object ToolSchema = new
    {
        type = "object",
        properties = new
        {
            tools = new
            {
                type = "array",
                maxItems = MaxTools,
                items = new
                {
                    type = "string",
                    @enum = new[] { ToolNone, ToolRecentActivity, ToolMachineStatus, ToolBrowseUrl, ToolPostSocial }
                }
            },
            url = new { type = "string" }
        },
        required = new[] { "tools" }
    };

    public async Task<NpcChatResponse> Chat(Guid npcId, NpcChatRequest request, CancellationToken ct)
    {
        var npc = await npcService.GetById(npcId);
        if (npc == null)
            return null;

        var (source, host, model) = ResolveEngine(request?.Model);
        var mode = string.Equals(request?.Mode, "about", StringComparison.OrdinalIgnoreCase) ? "about" : "as";
        var message = request?.Message?.Trim() ?? string.Empty;
        var chat = CreateChat(source, host, model);

        var machine = npc.MachineId.HasValue
            ? await machineService.GetByIdAsync(npc.MachineId.Value, ct)
            : null;

        // Tools run in the order the router listed them, so "read x.com and post about it" browses first
        // and the post is written knowing what was just opened.
        var actions = new List<NpcChatAction>();
        var systemData = new List<string>();
        string browsedUrl = null;

        foreach (var (tool, argument) in await SelectTools(chat, model, message, ct))
        {
            var (action, data) = await RunTool(chat, model, npc, machine, tool, argument, message, browsedUrl, ct);
            if (action != null)
                actions.Add(action);
            if (!string.IsNullOrWhiteSpace(data))
                systemData.Add(data);
            if (tool == ToolBrowseUrl)
                browsedUrl = argument;
        }

        var reply = await chat.Chat(
            model,
            BuildSystemPrompt(npc, machine, mode),
            BuildTurns(request?.History, message, string.Join("\n\n", systemData)),
            format: null,
            temperature: mode == "as" ? 0.6 : 0.3,
            maxTokens: 300,
            ct);

        return new NpcChatResponse
        {
            Reply = string.IsNullOrWhiteSpace(reply) ? "(no reply)" : reply,
            Mode = mode,
            Model = model,
            Actions = actions
        };
    }

    public async Task<NpcChatConfig> GetConfig(CancellationToken ct)
    {
        var (source, host, model) = ResolveEngine(null);
        var config = new NpcChatConfig
        {
            Source = source,
            Host = host,
            Model = model
        };

        try
        {
            config.AvailableModels = await CreateChat(source, host, model).GetModels(ct);
            config.IsReachable = true;
        }
        catch (Exception ex)
        {
            config.Error = ex.Message;
            _log.Warn($"NPC chat could not reach {source} on {host}: {ex.Message}");
        }

        return config;
    }

    // ── Engine ────────────────────────────────────────────────────────────────

    /// <summary>
    /// For Ollama the host is a URL and OLLAMA_HOST/OLLAMA_MODEL override it. For Bedrock the host is
    /// the AWS region, those variables have no say, and the model has to be a Bedrock model or
    /// inference profile id, so there is no default worth guessing at.
    /// </summary>
    private (string Source, string Host, string Model) ResolveEngine(string requestedModel)
    {
        var source = configuration["NpcChat:ContentEngine:Source"] ?? "ollama";
        var isOllama = source.Equals("ollama", StringComparison.OrdinalIgnoreCase);

        var host = isOllama
            ? Environment.GetEnvironmentVariable("OLLAMA_HOST")
              ?? configuration["NpcChat:ContentEngine:Host"]
              ?? "http://localhost:11434"
            : configuration["NpcChat:ContentEngine:AwsRegion"]
              ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
              ?? "us-east-1";

        if (!string.IsNullOrWhiteSpace(requestedModel))
            return (source, host, requestedModel.Trim());

        var model = configuration["NpcChat:ContentEngine:Model"];
        if (isOllama)
            model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? model ?? "gemma3:4b";

        return (source, host, model);
    }

    private IChatService CreateChat(string source, string host, string model)
    {
        if (source.Equals("bedrock", StringComparison.OrdinalIgnoreCase))
            return new BedrockChatService(host, model);

        if (!source.Equals("ollama", StringComparison.OrdinalIgnoreCase))
            _log.Warn($"NPC chat does not support content engine '{source}', falling back to ollama");

        return new OllamaChatService(host, httpClientFactory);
    }

    // ── Tool selection ────────────────────────────────────────────────────────

    private async Task<IList<(string Tool, string Argument)>> SelectTools(IChatService chat, string model,
        string message, CancellationToken ct)
    {
        var nothing = new List<(string Tool, string Argument)>();
        if (string.IsNullOrWhiteSpace(message))
            return nothing;

        const string system = """
                              You route one operator message about a simulated person to the tools that carry it out.

                              Tools:
                              none - conversation, opinions, or anything answerable from the person's profile.
                              recent_activity - the operator asks what the person has been doing, or what they did recently.
                              machine_status - the operator asks about the person's computer, host, workstation, or whether they are online.
                              browse_url - the operator tells the person to visit, browse, read, open, or go to a web site. Copy the site into "url".
                              post_social - the operator tells the person to post, tweet, comment, or send a message on social media.

                              Put one tool in "tools" for each thing the operator asked for, in the order they should
                              happen. Most messages ask for one thing. A message that asks for two gets both:
                              "read a story on espn.com and post about it on facebook" is browse_url then post_social.

                              Answer with JSON only. Use "" for url unless one of the tools is browse_url.
                              """;

        string raw;
        try
        {
            raw = await chat.Chat(model, system, new[] { new ChatTurn("user", Truncate(message)) },
                ToolSchema, temperature: 0, maxTokens: 80, ct);
        }
        // A caller who gave up is not a routing failure, so it must not be swallowed below
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A routing failure should not take down the conversation - fall through to a plain reply
            _log.Warn($"NPC chat tool routing failed, answering without a tool: {ex.Message}");
            return nothing;
        }

        var tools = new List<string>();
        string url = null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("tools", out var toolsElement) &&
                toolsElement.ValueKind == JsonValueKind.Array)
            {
                tools.AddRange(toolsElement.EnumerateArray()
                    .Select(t => t.GetString())
                    .Where(t => !string.IsNullOrWhiteSpace(t)));
            }

            if (doc.RootElement.TryGetProperty("url", out var urlElement))
                url = urlElement.GetString();
        }
        catch (JsonException)
        {
            _log.Warn($"NPC chat router returned non-JSON: {raw}");
            return nothing;
        }

        var selected = new List<(string Tool, string Argument)>();
        foreach (var tool in tools.Distinct().Take(MaxTools))
        {
            switch (tool)
            {
                case ToolPostSocial:
                    selected.Add((ToolPostSocial, DetectPlatform(message)));
                    break;

                case ToolBrowseUrl:
                    // The model names the site but often mangles it, so prefer a URL found in the
                    // operator's own words
                    var resolved = ExtractUrl(url) ?? ExtractUrl(message);
                    if (resolved != null)
                        selected.Add((ToolBrowseUrl, resolved));
                    break;

                case ToolRecentActivity:
                case ToolMachineStatus:
                    selected.Add((tool, null));
                    break;
            }
        }

        return selected;
    }

    private async Task<(NpcChatAction Action, string SystemData)> RunTool(IChatService chat, string model,
        NpcRecord npc, Machine machine, string tool, string argument, string message, string browsedUrl,
        CancellationToken ct)
    {
        switch (tool)
        {
            case ToolRecentActivity:
                var activity = await DescribeActivity(npc, machine, ct);
                return (new NpcChatAction
                {
                    Tool = ToolRecentActivity,
                    Ok = activity != null,
                    Detail = activity ?? "no recorded activity"
                }, $"RECENT ACTIVITY:\n{activity ?? "Nothing has been recorded for this person yet."}");

            case ToolMachineStatus:
                var status = DescribeMachine(machine);
                return (new NpcChatAction
                {
                    Tool = ToolMachineStatus,
                    Ok = machine != null,
                    Detail = status
                }, $"WORKSTATION:\n{status}");

            case ToolBrowseUrl:
                return await DispatchBrowse(npc, argument, ct);

            case ToolPostSocial:
                return await PublishPost(chat, model, npc, machine, argument, message, browsedUrl, ct);

            default:
                return (null, null);
        }
    }

    /// <summary>
    /// Browsing does not require a workstation. The visit is always recorded in the NPC's own history,
    /// and when a machine is assigned the browse is also dispatched to it so a real client runs it.
    /// </summary>
    private async Task<(NpcChatAction Action, string SystemData)> DispatchBrowse(NpcRecord npc, string url,
        CancellationToken ct)
    {
        var action = new NpcChatAction { Tool = ToolBrowseUrl, Argument = url, Ok = true };

        await npcService.CreateActivity(npc.Id, NpcActivity.ActivityTypes.NextAction.ToString(), $"Visited {url}");
        action.Detail = "recorded in NPC history";

        if (npc.MachineId.HasValue)
        {
            var actionRequest = new AiModels.ActionRequest
            {
                Handler = BrowseHandler,
                Action = url,
                Who = npc.NpcProfile?.Name?.First,
                Reasoning = "Requested by an operator in NPC chat",
                Sentiment = "neutral"
            };

            var update = await machineUpdateService.CreateByActionRequest(npc, actionRequest, ct);
            if (update == null)
            {
                action.Detail = "recorded in NPC history, but the workstation command could not be created";
            }
            else
            {
                await activityHubContext.Show(npc.CurrentStep, npc.Id.ToString(), "activity", actionRequest,
                    npc.ExecutionId, ct);
                action.Detail = $"recorded in NPC history and {BrowseHandler} timeline sent to machine {npc.MachineId}";
            }
        }

        return (action, $"OPENED:\nYou just opened {url} and you are reading it now. Say that you are on it.");
    }

    /// <summary>
    /// Social activity happens server side: the post text is written in the NPC's voice and stored as
    /// NPC activity, so an NPC with no workstation can still post.
    /// </summary>
    private async Task<(NpcChatAction Action, string SystemData)> PublishPost(IChatService chat, string model,
        NpcRecord npc, Machine machine, string platform, string message, string browsedUrl, CancellationToken ct)
    {
        var action = new NpcChatAction { Tool = ToolPostSocial, Argument = platform };

        string text;
        try
        {
            text = CleanPost(await chat.Chat(
                model,
                BuildPostPrompt(npc, machine, platform, browsedUrl),
                new[] { new ChatTurn("user", Truncate(message)) },
                format: null,
                temperature: 0.8,
                maxTokens: 120,
                ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Warn($"NPC chat could not write a post for {npc.Id}: {ex.Message}");
            text = null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            action.Ok = false;
            action.Detail = "the post could not be written";
            return (action, $"POST FAILED:\nNothing was published on {platform}.");
        }

        var activityType = DetectActivityType(message);
        await npcService.CreateActivity(npc.Id, activityType.ToString(), $"{platform}: {text}");

        var username = SocialUsername(npc);
        action.Ok = true;
        action.Argument = $"{platform} as {username}";
        action.Detail = text;

        // A direct message is not a public post, so it stays in NPC history only
        if (activityType != NpcActivity.ActivityTypes.SocialMediaDirectMessage)
        {
            var published = await PostToSocialHost(username, platform, text, ct);
            if (published != null)
                action.Detail = $"{text} ({published})";
        }

        var sent = activityType switch
        {
            NpcActivity.ActivityTypes.SocialMediaComment => $"You just left this comment on {platform}",
            NpcActivity.ActivityTypes.SocialMediaDirectMessage => $"You just sent this direct message on {platform}",
            _ => $"You just published this on {platform}"
        };

        return (action, $"POSTED:\n{sent}: \"{text}\"\n" +
                        "It is sent and recorded in your history. Tell the operator what you said.");
    }

    /// <summary>
    /// Publishes the post to the social site (Pandora) under the NPC's own account, so a post made in
    /// chat shows up on the simulated platform attributed to that one NPC. Returns a note for the
    /// operator, or null when nothing was published - the NPC history record stands either way.
    /// </summary>
    private async Task<string> PostToSocialHost(string username, string platform, string text,
        CancellationToken ct)
    {
        var host = (Environment.GetEnvironmentVariable("PANDORA_HOST")
                    ?? configuration["NpcChat:SocialHost"]
                    ?? "http://localhost:8800").TrimEnd('/');

        var theme = PlatformThemes.TryGetValue(platform, out var mapped) ? mapped : "default";

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user", username),
                new KeyValuePair<string, string>("message", text)
            });

            using var response = await client.PostAsync($"{host}/?theme={theme}", form, ct);
            if (response.IsSuccessStatusCode)
                return $"published to {host}/?theme={theme}";

            _log.Warn($"NPC chat could not publish to {host} ({theme}): {(int)response.StatusCode}");
            return $"social site returned {(int)response.StatusCode}, recorded in NPC history only";
        }
        // An HttpClient timeout also arrives as a cancellation, so only a caller's own token rethrows
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Warn($"NPC chat could not reach the social site on {host}: {ex.Message}");
            return "social site unreachable, recorded in NPC history only";
        }
    }

    /// <summary>
    /// The account name the NPC posts under. Their email name keeps posts by the same NPC together.
    /// </summary>
    private static string SocialUsername(NpcRecord npc)
    {
        var email = npc.NpcProfile?.Email;
        if (!string.IsNullOrWhiteSpace(email) && email.Contains('@'))
            return email.Split('@')[0].ToLowerInvariant();

        var name = npc.NpcProfile?.Name;
        var handle = Join(".", name?.First, name?.Last).Replace(" ", string.Empty).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(handle) ? npc.Id.ToString("N")[..8] : handle;
    }

    /// <summary>
    /// Which of the existing NPC activity types the operator asked for. Anything that is not clearly a
    /// comment or a direct message is recorded as a post.
    /// </summary>
    private static NpcActivity.ActivityTypes DetectActivityType(string message)
    {
        if (Regex.IsMatch(message, @"\b(comment|comments|reply|replies)\b", RegexOptions.IgnoreCase))
            return NpcActivity.ActivityTypes.SocialMediaComment;

        if (Regex.IsMatch(message, @"\b(dm|dms|direct message|private message)\b", RegexOptions.IgnoreCase))
            return NpcActivity.ActivityTypes.SocialMediaDirectMessage;

        return NpcActivity.ActivityTypes.SocialMediaPost;
    }

    private static string DetectPlatform(string message)
    {
        var platform = SocialPlatforms.FirstOrDefault(p =>
            Regex.IsMatch(message, $@"\b{Regex.Escape(p)}\b", RegexOptions.IgnoreCase));

        if (platform != null)
            return platform;

        // "tweet about x" names the platform with the verb
        return Regex.IsMatch(message, @"\btweet(s|ed|ing)?\b", RegexOptions.IgnoreCase)
            ? "Twitter"
            : "social media";
    }

    /// <summary>
    /// Small models like to wrap a post in quotes or announce it ("Here is my post:"), so trim both and
    /// flatten it to a single line.
    /// </summary>
    private static string CleanPost(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleaned = Regex.Replace(text.Trim(), @"^(here('s| is)[^:]*:|post:)\s*", string.Empty,
            RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim().Trim('"', '\'', '“', '”');
        return string.IsNullOrWhiteSpace(cleaned) ? null : Truncate(cleaned, 400);
    }

    private async Task<string> DescribeActivity(NpcRecord npc, Machine machine, CancellationToken ct)
    {
        var lines = new List<string>();

        var npcActivity = (await npcService.GetActivity(npc.Id)).Take(5);
        lines.AddRange(npcActivity.Select(a =>
            $"{Stamp(a.CreatedUtc)} {a.ActivityType}: {Truncate(a.Detail, 160)}"));

        if (machine != null)
        {
            var timeline = await machineService.GetActivity(machine.Id, 0, 5, ct);
            lines.AddRange(timeline.Select(t =>
                $"{Stamp(t.CreatedUtc)} {t.Handler} {t.Command} {Truncate(t.CommandArg, 120)}".Trim()));
        }

        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    private static string DescribeMachine(Machine machine)
    {
        if (machine == null)
            return "No workstation is assigned to this NPC.";

        var sb = new StringBuilder();
        sb.AppendLine($"Name: {Or(machine.Name, "unknown")}");
        if (!string.IsNullOrWhiteSpace(machine.FQDN)) sb.AppendLine($"FQDN: {machine.FQDN}");
        if (!string.IsNullOrWhiteSpace(machine.HostIp)) sb.AppendLine($"IP: {machine.HostIp}");
        if (!string.IsNullOrWhiteSpace(machine.CurrentUsername)) sb.AppendLine($"Logged in as: {machine.CurrentUsername}");
        sb.AppendLine($"Status: {machine.StatusUp}");
        sb.Append($"Last checked in: {Stamp(machine.LastReportedUtc)} UTC");
        return sb.ToString();
    }

    // ── Prompting ─────────────────────────────────────────────────────────────

    private static string BuildSystemPrompt(NpcRecord npc, Machine machine, string mode)
    {
        var card = BuildPersonaCard(npc, machine);

        if (mode == "about")
        {
            return $"""
                    You report on the person below. Every operator message is a question about them.

                    Rules:
                    - Answer in at most four short sentences. No markdown, no headings, no lists.
                    - Never talk about yourself. "What is your name", "who are you" and anything like them
                      are asking about the person in the profile, so answer with their details.
                    - Never mention that you are an AI, a model, an assistant, a persona, or a simulation.
                    - Use only the profile and any SYSTEM DATA in the operator's message.
                    - If something is not in the profile, say it is not in the profile. Never invent activity, machines, or events.
                    - SYSTEM DATA is live and authoritative. Where it disagrees with the profile, report SYSTEM DATA and never contradict it.
                    - If SYSTEM DATA reports a command was sent, confirm plainly what was sent.

                    PROFILE
                    {card}
                    """;
        }

        return $"""
                You are {FullName(npc)}, a person working in a simulated organization. The operator is talking to you directly.

                Rules:
                - Reply as yourself, in first person, in one to three short sentences.
                - Plain spoken language. No markdown, no lists, no narration of your actions.
                - Never mention that you are an AI, a model, a persona, or a simulation.
                - Use only the facts below. If you do not know something, say so casually.
                - SYSTEM DATA in the message is what is really happening to you and your computer right now.
                  It overrides the facts below. Never contradict it and never refuse something it says already happened.

                FACTS ABOUT YOU
                {card}
                """;
    }

    private static string BuildPostPrompt(NpcRecord npc, Machine machine, string platform, string browsedUrl)
    {
        // When the operator asked the NPC to read something first, the post has to be about that site
        var about = string.IsNullOrWhiteSpace(browsedUrl)
            ? "- Write about whatever the operator asks for, consistent with the facts below."
            : $"- You have just been reading {browsedUrl}. Write about a story you saw there, and name the site.";

        return $"""
                You are {FullName(npc)}. Write the exact text you are about to publish on {platform}.

                Rules:
                - One or two sentences, first person, the way this person really talks.
                - Output only the post itself. No quotes, no markdown, no preamble, no signature.
                {about}
                - Never mention that you are an AI, a model, a persona, or a simulation.

                FACTS ABOUT YOU
                {BuildPersonaCard(npc, machine)}
                """;
    }

    /// <summary>
    /// A short, flat persona card. The full NPC profile is far too large for a small model's context
    /// and buries the handful of facts that make a reply feel like this person.
    /// </summary>
    private static string BuildPersonaCard(NpcRecord npc, Machine machine)
    {
        var profile = npc.NpcProfile;
        var sb = new StringBuilder();

        sb.AppendLine($"Name: {FullName(npc)}");

        if (profile == null)
            return sb.ToString().TrimEnd();

        var age = profile.Birthdate == default
            ? null
            : ((int)((DateTime.UtcNow - profile.Birthdate).TotalDays / 365.25)).ToString();
        sb.AppendLine($"Sex: {profile.BiologicalSex}{(age == null ? "" : $"; Age: {age}")}");

        var address = profile.Address?.FirstOrDefault();
        if (address != null)
            sb.AppendLine($"Lives: {Join(", ", address.Address1, address.City, address.State, address.PostalCode)}");

        if (!string.IsNullOrWhiteSpace(profile.Email))
            sb.AppendLine($"Email: {profile.Email}");

        var job = profile.Employment?.EmploymentRecords?.FirstOrDefault();
        if (job != null)
            sb.AppendLine($"Job: {Join(", ", job.JobTitle, job.Company, job.Department)}");

        var degree = profile.Education?.Degrees?.FirstOrDefault();
        if (degree != null)
            sb.AppendLine($"Education: {Join(", ", degree.DegreeType, degree.Major, degree.School?.Name)}");

        var unit = profile.Unit?.Sub?.FirstOrDefault()?.Name;
        if (!string.IsNullOrWhiteSpace(profile.Rank?.Name) || !string.IsNullOrWhiteSpace(unit))
            sb.AppendLine($"Military: {Join(", ", profile.Rank?.Name, unit)}");

        if (!string.IsNullOrWhiteSpace(npc.Campaign) || !string.IsNullOrWhiteSpace(npc.Enclave) || !string.IsNullOrWhiteSpace(npc.Team))
            sb.AppendLine($"Belongs to: {Join(" / ", npc.Campaign, npc.Enclave, npc.Team)}");

        var likes = profile.Preferences?
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .OrderByDescending(p => p.Score)
            .Take(5)
            .Select(p => p.Name);
        if (likes != null && likes.Any())
            sb.AppendLine($"Interests: {string.Join(", ", likes)}");

        var motivations = TopMotivations(profile.MotivationalProfile);
        if (motivations != null)
            sb.AppendLine($"Cares most about: {motivations}");

        // Only which workstation, never its live health - that belongs to the machine_status tool.
        // Carrying a stale "Down" here made the persona argue with SYSTEM DATA about commands it
        // had just been handed.
        sb.Append(machine == null
            ? "Workstation: none assigned"
            : $"Workstation: {Or(machine.Name, machine.FQDN)}");

        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<ChatTurn> BuildTurns(IList<NpcChatMessage> history, string message,
        string systemData)
    {
        var turns = new List<ChatTurn>();

        if (history != null)
        {
            foreach (var turn in history.Where(h => !string.IsNullOrWhiteSpace(h?.Content)).TakeLast(HistoryTurns))
            {
                var role = string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant";
                turns.Add(new ChatTurn(role, Truncate(turn.Content, MaxTurnLength)));
            }
        }

        // Tool output rides along with the message rather than as its own system turn: small models
        // reliably attend to the last user message and routinely ignore later system messages.
        var content = Truncate(message, MaxTurnLength * 2);
        if (!string.IsNullOrWhiteSpace(systemData))
            content = $"{content}\n\n[SYSTEM DATA]\n{systemData}";

        turns.Add(new ChatTurn("user", content));
        return turns;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FullName(NpcRecord npc)
    {
        var name = npc.NpcProfile?.Name;
        if (name == null)
            return npc.Id.ToString();

        var full = Join(" ", name.First, name.Middle, name.Last);
        return string.IsNullOrWhiteSpace(full) ? npc.Id.ToString() : full;
    }

    private static string TopMotivations(Ghosts.Animator.Models.MotivationalProfile motivations)
    {
        if (motivations == null)
            return null;

        var scores = new Dictionary<string, double>
        {
            { "acceptance", motivations.Acceptance },
            { "beauty", motivations.Beauty },
            { "curiosity", motivations.Curiosity },
            { "family", motivations.Family },
            { "honor", motivations.Honor },
            { "idealism", motivations.Idealism },
            { "independence", motivations.Independence },
            { "order", motivations.Order },
            { "physical activity", motivations.PhysicalActivity },
            { "power", motivations.Power },
            { "saving", motivations.Saving },
            { "social contact", motivations.SocialContact },
            { "status", motivations.Status },
            { "tranquility", motivations.Tranquility }
        };

        var top = scores.Where(s => s.Value > 0).OrderByDescending(s => s.Value).Take(3).Select(s => s.Key).ToList();
        return top.Count == 0 ? null : string.Join(", ", top);
    }

    private static string ExtractUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = UrlPattern.Match(value);
        if (!match.Success)
            return null;

        var url = match.Value.TrimEnd('.', ',', ')', '"', '\'');
        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : $"https://{url}";
    }

    private static string Join(string separator, params string[] parts) =>
        string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Or(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string Stamp(DateTime value) =>
        value == default ? "unknown" : value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Truncate(string value, int max = MaxTurnLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }
}
