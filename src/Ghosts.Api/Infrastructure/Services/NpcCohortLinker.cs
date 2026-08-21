// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using Ghosts.Animator;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Api.Infrastructure.Services;

/// <summary>
/// NPCs are generated one at a time and have no idea the rest of their cohort exists, which leaves
/// both NpcProfile.Relationships and the social graph empty until an animation or n8n workflow runs.
/// This wires each member of a freshly generated cohort to a handful of peers.
/// </summary>
public static class NpcCohortLinker
{
    private const int MaxConnectionsPerNpc = 3;

    private static readonly string[] RelationshipTypes =
        { "Coworker", "Teammate", "Supervisor", "Direct Report", "Friend", "Neighbor" };

    /// <summary>
    /// Adds reciprocal social connections between cohort members and mirrors them onto each
    /// NpcProfile.Relationships. Rows are added to the context only — the caller saves.
    /// </summary>
    public static void Link(DbContext context, IList<NpcRecord> cohort)
    {
        if (cohort == null || cohort.Count < 2) return;

        var linked = new HashSet<(Guid, Guid)>();
        var take = Math.Min(MaxConnectionsPerNpc, cohort.Count - 1);

        foreach (var npc in cohort)
        {
            var peers = cohort.Where(x => x.Id != npc.Id).Shuffle(AnimatorRandom.Rand).Take(take);
            foreach (var peer in peers)
            {
                var pair = npc.Id.CompareTo(peer.Id) < 0 ? (npc.Id, peer.Id) : (peer.Id, npc.Id);
                if (!linked.Add(pair)) continue;

                var type = RelationshipTypes.RandomElement();
                // skewed positive, on the -1.0 (bad) to 1.0 (perfect) scale
                var status = Math.Round((decimal)(AnimatorRandom.Rand.NextDouble() * 1.4 - 0.4), 2);

                AddEdge(context, npc, peer, type, status);
                AddEdge(context, peer, npc, type, status);
            }
        }
    }

    private static void AddEdge(DbContext context, NpcRecord from, NpcRecord to, string type,
        decimal status)
    {
        context.Set<NpcSocialConnection>().Add(new NpcSocialConnection
        {
            Id = Guid.NewGuid().ToString(),
            NpcId = from.Id,
            ConnectedNpcId = to.Id,
            Name = to.NpcProfile?.Name?.ToString() ?? string.Empty,
            Distance = "1",
            RelationshipStatus = status,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });

        if (from.NpcProfile == null) return;

        var relationships = from.NpcProfile.Relationships?.ToList() ?? new List<RelationshipProfile>();
        relationships.Add(new RelationshipProfile { Id = relationships.Count, With = to.Id, Type = type });
        from.NpcProfile.Relationships = relationships;

        // the profile is a jsonb column, so a mutation inside it is not picked up on its own
        var entry = context.Entry(from);
        if (entry.State != EntityState.Added)
            entry.Property(x => x.NpcProfile).IsModified = true;
    }
}
