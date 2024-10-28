// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions.Chat;

public class ChatJobConfiguration
{
    public ChatPlatformConfiguration Chat { get; set; }
    public List<string> Replacements { get; set; }
    public List<string> Drops { get; set; }

    public List<string> Prompts { get; set; }

    public class ChatPlatformConfiguration
    {
        public string BaseUrl { get; set; }
        public string AdminUsername { get; set; }
        public string AdminPassword { get; set; }
        public string DefaultUserPassword { get; set; }
        public int AgentsPerBatch { get; set; }
    }
}
