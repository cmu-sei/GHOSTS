// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using Ghosts.Api.Infrastructure.Extensions;
using NLog;

namespace Ghosts.Api.Infrastructure.ContentServices.Bedrock;

/// <summary>
/// Multi-turn chat against a Bedrock hosted model over the Converse API, the counterpart to
/// OllamaChatService. The model id is passed through verbatim, so it must be a model or inference
/// profile id the AWS account has access to. Credentials are never read from configuration.
/// </summary>
public class BedrockChatService(string region, string configuredModel) : IChatService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Models fence JSON now and then even when told not to
    /// </summary>
    private static readonly Regex JsonFence = new(@"^```(?:json)?\s*(.*?)\s*```$", RegexOptions.Singleline);

    public async Task<string> Chat(string model, string system, IEnumerable<ChatTurn> turns, object format,
        double temperature, int maxTokens, CancellationToken ct)
    {
        // Converse has no equivalent of Ollama's schema constrained output, so the schema goes into the
        // system prompt instead. The router already falls back to a plain reply when it cannot parse.
        var systemPrompt = format == null
            ? system
            : $"{system}\n\nReply with JSON only, matching this schema. No markdown and no commentary:\n" +
              JsonSerializer.Serialize(format);

        // Temperature is deliberately not sent: newer Anthropic models on Bedrock reject it
        // ("`temperature` is deprecated for this model") and fail the whole Converse call.
        var request = new ConverseRequest
        {
            ModelId = model,
            Messages = BuildMessages(turns),
            InferenceConfig = new InferenceConfiguration
            {
                MaxTokens = maxTokens
            }
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
            request.System = [new SystemContentBlock { Text = systemPrompt }];

        ConverseResponse response;
        try
        {
            response = await BuildClient().ConverseAsync(request, ct);
        }
        // AmazonServiceException (e.g. ValidationException) is a sibling of AmazonClientException,
        // so both have to be caught or the raw AWS exception escapes as an unhandled 500
        catch (Exception ex) when (ex is AmazonClientException or AmazonServiceException)
        {
            _log.Error($"Bedrock chat failed in {region} with model {model.ToSafeLogValue()}: {ex.Message.ToSafeLogValue()}");
            throw new InvalidOperationException($"Bedrock ({region}, {model}): {ex.Message}");
        }

        // Content is a union of block types, so take the first block that actually carries text
        var text = response.Output?.Message?.Content?.FirstOrDefault(c => c.Text != null)?.Text;
        if (text == null)
        {
            _log.Error($"Bedrock chat returned no text for model {model.ToSafeLogValue()}, stop reason {response.StopReason}");
            throw new InvalidOperationException($"Bedrock ({region}, {model}) returned an unexpected response.");
        }

        return Unfence(text);
    }

    /// <summary>
    /// The Converse runtime cannot list models - that is the Bedrock control plane API, which is a
    /// separate SDK package this project does not reference. Resolving credentials is the check that
    /// actually matters here, so do that and report the configured model.
    /// </summary>
    public Task<IEnumerable<string>> GetModels(CancellationToken ct)
    {
        ResolveCredentials();

        IEnumerable<string> models = string.IsNullOrWhiteSpace(configuredModel)
            ? []
            : [configuredModel];

        return Task.FromResult(models);
    }

    /// <summary>
    /// Converse requires the turns to start with the user and to alternate, where Ollama takes them in
    /// any order. Drop leading assistant turns and fold consecutive turns of the same role together.
    /// </summary>
    private static List<Message> BuildMessages(IEnumerable<ChatTurn> turns)
    {
        var merged = new List<ChatTurn>();

        foreach (var turn in turns)
        {
            var role = string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? "assistant"
                : "user";

            if (merged.Count == 0 && role == "assistant")
                continue;

            if (merged.Count > 0 && merged[^1].Role == role)
                merged[^1] = merged[^1] with { Content = $"{merged[^1].Content}\n\n{turn.Content}" };
            else
                merged.Add(new ChatTurn(role, turn.Content));
        }

        return merged.Select(t => new Message
        {
            Role = t.Role == "assistant" ? ConversationRole.Assistant : ConversationRole.User,
            Content = [new ContentBlock { Text = t.Content }]
        }).ToList();
    }

    private AmazonBedrockRuntimeClient BuildClient()
    {
        var endpoint = RegionEndpoint.GetBySystemName(region);
        return new AmazonBedrockRuntimeClient(ResolveCredentials(), endpoint);
    }

    /// <summary>
    /// Explicit keys win, otherwise the default credential chain (IAM role, instance profile,
    /// ~/.aws/credentials). Throws when nothing is configured, which is what the config endpoint
    /// reports back to the operator.
    /// </summary>
    private static AWSCredentials ResolveCredentials()
    {
        var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

        return !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey)
            ? new BasicAWSCredentials(accessKey, secretKey)
            : FallbackCredentialsFactory.GetCredentials();
    }

    private static string Unfence(string text)
    {
        var trimmed = text.Trim();
        var match = JsonFence.Match(trimmed);
        return match.Success ? match.Groups[1].Value.Trim() : trimmed;
    }
}
