// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;

namespace Ghosts.Api.Infrastructure.ContentServices;

public interface IFormatterService
{
    Task<string> GenerateNextAction(NpcRecord npc, string history);
    Task<string> GenerateTweet(NpcRecord npc);

    Task<string> ExecuteQuery(string prompt);
}
