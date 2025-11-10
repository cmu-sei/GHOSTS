// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

[Table("npcsocialgraphs")]
public class NpcSocialGraph
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // Use NPC Id as primary key
    public Guid Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; }

    public long CurrentStep { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    // Navigation property to parent NPC
    [ForeignKey("Id")]
    public virtual NpcRecord Npc { get; set; }

    // Navigation properties to child collections
    public virtual ICollection<NpcSocialConnection> Connections { get; set; }
    public virtual ICollection<NpcLearning> Knowledge { get; set; }
    public virtual ICollection<NpcBelief> Beliefs { get; set; }
    public virtual ICollection<NpcPreference> Preferences { get; set; }

    public NpcSocialGraph()
    {
        Connections = new List<NpcSocialConnection>();
        Knowledge = new List<NpcLearning>();
        Beliefs = new List<NpcBelief>();
        Preferences = new List<NpcPreference>();
        CreatedUtc = DateTime.UtcNow;
        UpdatedUtc = DateTime.UtcNow;
    }
}
