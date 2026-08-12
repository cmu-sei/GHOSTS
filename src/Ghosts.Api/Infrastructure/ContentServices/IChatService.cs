// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghosts.Api.Infrastructure.ContentServices;

/// <summary>
/// One turn of a conversation. Role is user or assistant.
/// </summary>
public record ChatTurn(string Role, string Content);

/// <summary>
/// Multi-turn chat with an optional JSON schema constraining the response. Implemented once per engine
/// so NPC chat can run against a local Ollama model or a Bedrock hosted model unchanged.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Sends a chat completion request and returns the assistant message content.
    /// </summary>
    /// <param name="model">Engine specific model id</param>
    /// <param name="system">System prompt</param>
    /// <param name="turns">Conversation turns, roles are user or assistant</param>
    /// <param name="format">Optional JSON schema object that constrains the response</param>
    /// <param name="temperature">Sampling temperature</param>
    /// <param name="maxTokens">Response cap</param>
    /// <param name="ct">Cancellation token</param>
    Task<string> Chat(string model, string system, IEnumerable<ChatTurn> turns, object format,
        double temperature, int maxTokens, CancellationToken ct);

    /// <summary>
    /// The models an operator can choose from for this engine.
    /// </summary>
    Task<IEnumerable<string>> GetModels(CancellationToken ct);
}
