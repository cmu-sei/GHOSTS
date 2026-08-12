// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Api.Infrastructure.Models;

public class NpcChatRequest
{
    /// <summary>
    /// What the operator just said
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Prior turns of this conversation, oldest first. The service only keeps the most recent few.
    /// </summary>
    public IList<NpcChatMessage> History { get; set; } = new List<NpcChatMessage>();

    /// <summary>
    /// "as" talks to the NPC in character, "about" asks an operator assistant about the NPC
    /// </summary>
    public string Mode { get; set; } = "as";

    /// <summary>
    /// Optional model override, e.g. gemma3:4b. Defaults to the configured NpcChat model.
    /// </summary>
    public string Model { get; set; }
}

public class NpcChatMessage
{
    /// <summary>
    /// "user" for the operator, "npc" for a prior reply
    /// </summary>
    public string Role { get; set; }

    public string Content { get; set; }
}

public class NpcChatResponse
{
    public string Reply { get; set; }
    public string Mode { get; set; }
    public string Model { get; set; }

    /// <summary>
    /// What was looked up or carried out, in the order it ran. Empty when the reply needed no lookup
    /// and dispatched no command.
    /// </summary>
    public IList<NpcChatAction> Actions { get; set; } = new List<NpcChatAction>();
}

public class NpcChatAction
{
    /// <summary>
    /// Tool the model chose: recent_activity, machine_status, or browse_url
    /// </summary>
    public string Tool { get; set; }

    /// <summary>
    /// Tool argument, e.g. the URL for browse_url
    /// </summary>
    public string Argument { get; set; }

    /// <summary>
    /// False when the lookup found nothing or the command could not be delivered
    /// </summary>
    public bool Ok { get; set; }

    public string Detail { get; set; }
}

public class NpcChatConfig
{
    public string Source { get; set; }
    public string Host { get; set; }
    public string Model { get; set; }
    public IEnumerable<string> AvailableModels { get; set; } = new List<string>();
    public bool IsReachable { get; set; }
    public string Error { get; set; }
}
