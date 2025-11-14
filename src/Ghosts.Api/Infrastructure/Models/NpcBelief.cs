// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Ghosts.Api.Infrastructure.Models;

[Table("npc_beliefs")]
[method: JsonConstructor]
public class NpcBelief(int id, Guid npcId, Guid toNpcId, Guid fromNpcId, string name, long step, decimal likelihood, decimal posterior)
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } = id;

    [Required]
    public Guid NpcId { get; set; } = npcId;

    [Required]
    public Guid ToNpcId { get; set; } = toNpcId;

    [Required]
    public Guid FromNpcId { get; set; } = fromNpcId;

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = name;

    public long Step { get; set; } = step;

    [Column(TypeName = "decimal(18,6)")]
    public decimal Likelihood { get; set; } = likelihood;

    [Column(TypeName = "decimal(18,6)")]
    public decimal Posterior { get; set; } = posterior;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("NpcId")]
    public virtual NpcRecord Npc { get; set; }

    public NpcBelief() : this(0, Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, 0, 0, 0) { }

    public override string ToString()
    {
        return $"{ToNpcId},{FromNpcId},{Name},{Step},{Likelihood},{Posterior}";
    }

    public static string ToHeader()
    {
        return "To,From,Name,Step,Likelihood,Posterior";
    }
}
