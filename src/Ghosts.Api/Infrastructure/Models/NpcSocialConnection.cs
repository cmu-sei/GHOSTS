// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

[Table("npcsocialconnections")]
public class NpcSocialConnection
{
    [Key]
    [MaxLength(100)]
    public string Id { get; set; }

    [Required]
    public Guid NpcId { get; set; }

    [Required]
    public Guid ConnectedNpcId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; }

    [MaxLength(50)]
    public string Distance { get; set; }

    public int RelationshipStatus { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    // Navigation properties
    [ForeignKey("NpcId")]
    public virtual NpcRecord Npc { get; set; }

    public virtual ICollection<NpcInteraction> Interactions { get; set; }

    public NpcSocialConnection()
    {
        Id = Guid.NewGuid().ToString();
        Interactions = new List<NpcInteraction>();
        CreatedUtc = DateTime.UtcNow;
        UpdatedUtc = DateTime.UtcNow;
    }
}
