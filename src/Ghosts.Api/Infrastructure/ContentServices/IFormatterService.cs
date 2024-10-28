// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;

namespace ghosts.api.Infrastructure.ContentServices;

public interface IFormatterService
{
    Task<string> GenerateNextAction(NpcRecord npc, string history);
    Task<string> GenerateTweet(NpcRecord npc);

    Task<string> ExecuteQuery(string prompt);
}
